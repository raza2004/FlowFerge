"""Document ingestion: bytes in, searchable chunks out."""

import logging
import uuid

from pgvector import Vector

from app.config import get_settings
from app.db import connection
from app.embeddings import get_embedder
from app.rag.chunking import chunk_pages
from app.rag.extract import extract_pages

log = logging.getLogger(__name__)


def ingest_document(
    *, tenant_id: uuid.UUID, document_id: uuid.UUID, document_name: str, data: bytes, content_type: str = ""
) -> tuple[int, int]:
    """Extract, chunk, embed and store one document. Returns (page_count, chunk_count).

    Re-ingesting the same document replaces its chunks, so a redelivered queue message (which
    RabbitMQ allows) never creates duplicates.
    """
    settings = get_settings()
    pages = extract_pages(data, document_name, content_type)
    chunks = chunk_pages(pages, settings.chunk_tokens, settings.chunk_overlap_tokens)
    if not chunks:
        raise ValueError("The document contains no readable text.")

    vectors = get_embedder().embed([c.content for c in chunks])

    with connection() as conn:
        with conn.transaction():
            conn.execute("DELETE FROM chunks WHERE tenant_id = %s AND document_id = %s", (tenant_id, document_id))
            with conn.cursor() as cur:
                cur.executemany(
                    """
                    INSERT INTO chunks (tenant_id, document_id, document_name, page, chunk_index, content, embedding)
                    VALUES (%s, %s, %s, %s, %s, %s, %s)
                    """,
                    [
                        (tenant_id, document_id, document_name, c.page, c.index, c.content, Vector(v))
                        for c, v in zip(chunks, vectors)
                    ],
                )
    log.info("ingested document", extra={"document_id": str(document_id), "chunks": len(chunks)})
    return len(pages), len(chunks)


def delete_document(tenant_id: uuid.UUID, document_id: uuid.UUID) -> int:
    with connection() as conn:
        cur = conn.execute("DELETE FROM chunks WHERE tenant_id = %s AND document_id = %s", (tenant_id, document_id))
        return cur.rowcount
