"""PostgreSQL access. Raw SQL on purpose: pgvector and full text search are SQL features and an
ORM would only hide them."""

from contextlib import contextmanager
from typing import Iterator

import psycopg
from pgvector.psycopg import register_vector
from psycopg_pool import ConnectionPool

from app.config import get_settings

_pool: ConnectionPool | None = None


def _configure(conn: psycopg.Connection) -> None:
    register_vector(conn)


def get_pool() -> ConnectionPool:
    global _pool
    if _pool is None:
        url = get_settings().database_url
        if not url:
            raise RuntimeError("DATABASE_URL is not set.")
        _pool = ConnectionPool(url, min_size=1, max_size=5, configure=_configure, open=True)
    return _pool


@contextmanager
def connection() -> Iterator[psycopg.Connection]:
    with get_pool().connection() as conn:
        yield conn


def init_schema() -> None:
    """Idempotent. Creates the vector extension, the chunks table and its indexes.

    The `documents` table belongs to the .NET API; this service only owns `chunks`. Each chunk
    carries tenant_id, and every query filters on it, so one tenant can never see another's text.
    """
    dims = get_settings().embedding_dimensions
    # The extension must exist before the pool registers the vector type on a connection.
    with psycopg.connect(get_settings().database_url, autocommit=True) as conn:
        conn.execute("CREATE EXTENSION IF NOT EXISTS vector")

    with connection() as conn:
        conn.execute(
            f"""
            CREATE TABLE IF NOT EXISTS chunks (
                id            BIGSERIAL PRIMARY KEY,
                tenant_id     UUID    NOT NULL,
                document_id   UUID    NOT NULL,
                document_name TEXT    NOT NULL,
                page          INTEGER NOT NULL,
                chunk_index   INTEGER NOT NULL,
                content       TEXT    NOT NULL,
                embedding     vector({dims}) NOT NULL,
                tsv           tsvector GENERATED ALWAYS AS (to_tsvector('english', content)) STORED
            )
            """
        )
        conn.execute("CREATE INDEX IF NOT EXISTS ix_chunks_tenant ON chunks (tenant_id, document_id)")
        # HNSW gives fast approximate nearest neighbour search by cosine distance.
        conn.execute(
            "CREATE INDEX IF NOT EXISTS ix_chunks_embedding ON chunks USING hnsw (embedding vector_cosine_ops)"
        )
        conn.execute("CREATE INDEX IF NOT EXISTS ix_chunks_tsv ON chunks USING gin (tsv)")
        conn.commit()
