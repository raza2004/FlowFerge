from typing import Protocol

from openai import OpenAI

from app.config import get_settings


class Embedder(Protocol):
    def embed(self, texts: list[str]) -> list[list[float]]: ...


def make_client() -> OpenAI:
    settings = get_settings()
    if not settings.openai_api_key:
        raise RuntimeError("OPENAI_API_KEY is not set. Copy .env.example to .env and fill it in.")
    # max_retries=0: the SDK would silently retry 429s itself and multiply load on a provider that is
    # already rate limiting us. We do our own, visible fallback across models instead (see llm.py).
    return OpenAI(api_key=settings.openai_api_key, base_url=settings.openai_base_url or None, max_retries=0)


class OpenAIEmbedder:
    """Embeds text with the configured embeddings endpoint, in batches."""

    BATCH = 64

    def embed(self, texts: list[str]) -> list[list[float]]:
        client = make_client()
        model = get_settings().embedding_model
        vectors: list[list[float]] = []
        for start in range(0, len(texts), self.BATCH):
            batch = texts[start : start + self.BATCH]
            response = client.embeddings.create(model=model, input=batch)
            vectors.extend(item.embedding for item in sorted(response.data, key=lambda d: d.index))
        return vectors


_embedder: Embedder | None = None


def get_embedder() -> Embedder:
    global _embedder
    if _embedder is None:
        _embedder = OpenAIEmbedder()
    return _embedder


def set_embedder(embedder: Embedder | None) -> None:
    """Swap the embedder (tests use a fake so they never hit the network)."""
    global _embedder
    _embedder = embedder


def embed_text(text: str) -> list[float]:
    """Return the embedding vector for one piece of text."""
    return get_embedder().embed([text])[0]


if __name__ == "__main__":
    # python -m app.embeddings
    vector = embed_text("Which tasks are overdue in the FlowForge board?")
    print(f"embedding length: {len(vector)}")
