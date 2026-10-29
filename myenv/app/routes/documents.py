import logging
import uuid

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile

from app.db import connection
from app.rag.ingest import delete_document, ingest_document
from app.security import RequestContext, require_context

router = APIRouter()
log = logging.getLogger(__name__)

MAX_BYTES = 25 * 1024 * 1024


@router.post("/documents")
async def upload_document(
    file: UploadFile = File(...),
    document_id: str | None = Form(default=None),
    ctx: RequestContext = Depends(require_context),
) -> dict:
    """Read, chunk, embed and store a document for the caller's tenant.

    The caller may choose the document id. Sending the same id again replaces the earlier chunks, so a
    repeated upload (a retry, a double click) never creates duplicates.
    """
    data = await file.read()
    if not data:
        raise HTTPException(status_code=400, detail="The file is empty.")
    if len(data) > MAX_BYTES:
        raise HTTPException(status_code=413, detail="Documents can be at most 25 MB.")

    try:
        doc_id = uuid.UUID(document_id) if document_id else uuid.uuid4()
    except ValueError:
        raise HTTPException(status_code=400, detail="document_id must be a UUID.")

    try:
        pages, chunks = ingest_document(
            tenant_id=ctx.tenant_id,
            document_id=doc_id,
            document_name=file.filename or "document",
            data=data,
            content_type=file.content_type or "",
        )
    except ValueError as ex:  # unsupported type, or no readable text
        raise HTTPException(status_code=400, detail=str(ex))

    return {"document_id": str(doc_id), "file_name": file.filename, "pages": pages, "chunks": chunks}


@router.get("/documents")
def list_documents(ctx: RequestContext = Depends(require_context)) -> list[dict]:
    """The caller's documents (one row each), newest first."""
    with connection() as conn:
        rows = conn.execute(
            """
            SELECT document_id, document_name, max(page) AS pages, count(*) AS chunks
            FROM chunks
            WHERE tenant_id = %s
            GROUP BY document_id, document_name
            ORDER BY min(id) DESC
            """,
            (ctx.tenant_id,),
        ).fetchall()
    return [{"document_id": str(r[0]), "file_name": r[1], "pages": r[2], "chunks": r[3]} for r in rows]


@router.delete("/documents/{document_id}")
def remove_document(document_id: uuid.UUID, ctx: RequestContext = Depends(require_context)) -> dict:
    """Remove a document's chunks. Scoped by tenant, so another tenant's id deletes nothing."""
    return {"deleted_chunks": delete_document(ctx.tenant_id, document_id)}
