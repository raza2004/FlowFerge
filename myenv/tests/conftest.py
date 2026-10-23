"""Shared test helpers: a fake embedder and a scripted fake LLM, so tests never touch the network."""

import hashlib
import math
import os
import re
import uuid

import pytest

from app import embeddings, llm
from app.config import get_settings
from app.llm import ChatResult, Usage


class HashEmbedder:
    """Bag of words hashed into a fixed size vector. Texts that share words end up close together,
    which is enough to test retrieval plumbing. (Real quality is measured by evals/run_eval.py.)"""

    def embed(self, texts: list[str]) -> list[list[float]]:
        dims = get_settings().embedding_dimensions
        out = []
        for text in texts:
            vec = [0.0] * dims
            for word in re.findall(r"[a-z0-9]+", text.lower()):
                digest = hashlib.md5(word.encode()).digest()
                vec[int.from_bytes(digest[:4], "little") % dims] += 1.0
            norm = math.sqrt(sum(v * v for v in vec)) or 1.0
            out.append([v / norm for v in vec])
        return out


class ScriptedLlm:
    """Returns pre-programmed results in order, and records every call."""

    def __init__(self, *results: ChatResult | str, stream_text: str = "ok"):
        self.results = [ChatResult(content=r, usage=Usage(10, 5, "fake")) if isinstance(r, str) else r for r in results]
        self.calls: list[dict] = []
        self.stream_text = stream_text

    def complete(self, messages, tools=None, model=None):
        self.calls.append({"messages": messages, "tools": tools})
        if len(self.results) > 1:
            return self.results.pop(0)
        return self.results[0]

    def stream(self, messages):
        self.calls.append({"messages": messages, "tools": None})
        for word in self.stream_text.split(" "):
            yield word + " "
        yield Usage(10, 5, "fake")


@pytest.fixture(autouse=True)
def fakes(monkeypatch):
    """Every test starts with the fake embedder, no internal key requirement and a clean LLM."""
    monkeypatch.setenv("INTERNAL_API_KEY", "")
    get_settings.cache_clear()
    embeddings.set_embedder(HashEmbedder())
    llm.set_llm(ScriptedLlm("I do not know"))
    yield
    embeddings.set_embedder(None)
    llm.set_llm(None)
    get_settings.cache_clear()


TEST_DATABASE_URL = os.environ.get("TEST_DATABASE_URL", "")

requires_db = pytest.mark.skipif(not TEST_DATABASE_URL, reason="set TEST_DATABASE_URL to a pgvector Postgres to run")


@pytest.fixture
def db(monkeypatch):
    """A clean chunks table in a real pgvector Postgres."""
    monkeypatch.setenv("DATABASE_URL", TEST_DATABASE_URL)
    get_settings.cache_clear()
    import app.db as dbmod

    dbmod._pool = None
    dbmod.init_schema()
    with dbmod.connection() as conn:
        conn.execute("TRUNCATE chunks")
        conn.commit()
    yield dbmod
    if dbmod._pool is not None:
        dbmod._pool.close()
        dbmod._pool = None


@pytest.fixture
def tenant_a() -> uuid.UUID:
    return uuid.uuid4()


@pytest.fixture
def tenant_b() -> uuid.UUID:
    return uuid.uuid4()
