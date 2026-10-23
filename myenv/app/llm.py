"""Chat completions with a model fallback chain, streaming and usage accounting."""

from dataclasses import dataclass, field
from typing import Any, Iterator, Protocol

import openai

from app.config import get_settings
from app.embeddings import make_client

# Statuses where trying the next model is worthwhile (rate limit, provider hiccup, model gone).
_RETRYABLE = {400, 402, 404, 408, 429, 500, 502, 503, 504}


@dataclass
class Usage:
    prompt_tokens: int = 0
    completion_tokens: int = 0
    model: str = ""

    @property
    def total_tokens(self) -> int:
        return self.prompt_tokens + self.completion_tokens

    @property
    def cost_usd(self) -> float:
        s = get_settings()
        return (
            self.prompt_tokens * s.price_input_per_million + self.completion_tokens * s.price_output_per_million
        ) / 1_000_000


@dataclass
class ChatResult:
    content: str = ""
    tool_calls: list[dict[str, Any]] = field(default_factory=list)
    usage: Usage = field(default_factory=Usage)


class LlmUnavailable(RuntimeError):
    """No configured model could answer."""


class Llm(Protocol):
    def complete(self, messages: list[dict], tools: list[dict] | None = None, model: str | None = None) -> ChatResult: ...

    def stream(self, messages: list[dict]) -> Iterator[str | Usage]: ...


def _model_chain(primary: str | None = None) -> list[str]:
    s = get_settings()
    chain = [primary or s.chat_model]
    chain += [m.strip() for m in s.chat_fallback_models.split(",") if m.strip()]
    return chain


class OpenAiLlm:
    def complete(self, messages: list[dict], tools: list[dict] | None = None, model: str | None = None) -> ChatResult:
        client = make_client()
        last_error: Exception | None = None
        for name in _model_chain(model):
            try:
                kwargs: dict[str, Any] = {"model": name, "messages": messages, "temperature": 0}
                if tools:
                    kwargs["tools"] = tools
                response = client.chat.completions.create(**kwargs)
            except openai.APIStatusError as ex:
                last_error = ex
                if ex.status_code in _RETRYABLE:
                    continue
                raise
            except openai.APIConnectionError as ex:
                last_error = ex
                continue

            choice = response.choices[0].message
            calls = [
                {"id": c.id, "name": c.function.name, "arguments": c.function.arguments}
                for c in (choice.tool_calls or [])
            ]
            usage = Usage(
                prompt_tokens=getattr(response.usage, "prompt_tokens", 0) or 0,
                completion_tokens=getattr(response.usage, "completion_tokens", 0) or 0,
                model=name,
            )
            if not (choice.content or calls):
                continue  # empty answer, try the next model
            return ChatResult(content=choice.content or "", tool_calls=calls, usage=usage)
        raise LlmUnavailable(f"No AI model could answer: {last_error}")

    def stream(self, messages: list[dict]) -> Iterator[str | Usage]:
        """Yields text deltas, then one Usage object at the end."""
        client = make_client()
        last_error: Exception | None = None
        for name in _model_chain():
            produced = False
            try:
                response = client.chat.completions.create(
                    model=name,
                    messages=messages,
                    temperature=0,
                    stream=True,
                    stream_options={"include_usage": True},
                )
                usage = Usage(model=name)
                for event in response:
                    if getattr(event, "usage", None):
                        usage.prompt_tokens = event.usage.prompt_tokens or 0
                        usage.completion_tokens = event.usage.completion_tokens or 0
                    if event.choices and event.choices[0].delta.content:
                        produced = True
                        yield event.choices[0].delta.content
                if produced:
                    yield usage
                    return
            except (openai.APIStatusError, openai.APIConnectionError) as ex:
                last_error = ex
                if produced:  # half an answer was already sent, cannot switch models now
                    raise
                continue
        raise LlmUnavailable(f"No AI model could answer: {last_error}")


_llm: Llm | None = None


def get_llm() -> Llm:
    global _llm
    if _llm is None:
        _llm = OpenAiLlm()
    return _llm


def set_llm(llm: Llm | None) -> None:
    global _llm
    _llm = llm
