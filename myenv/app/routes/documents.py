import uuid

from fastapi import APIRouter, Depends

from app.rag.ingest import delete_document
from app.security import RequestContext, require_context

router = APIRouter()


@router.delete("/documents/{document_id}")
def remove_document(document_id: uuid.UUID, ctx: RequestContext = Depends(require_context)) -> dict:
    """Remove a document's chunks. Scoped by tenant, so another tenant's id deletes nothing."""
    return {"deleted_chunks": delete_document(ctx.tenant_id, document_id)}
