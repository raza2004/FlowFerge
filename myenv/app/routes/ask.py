import logging

from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse

from app.rag.answer import answer_question, stream_answer
from app.schemas import AskRequest, AskResponse
from app.security import RequestContext, require_context

router = APIRouter()
log = logging.getLogger(__name__)


@router.post("/ask", response_model=AskResponse)
def ask(request: AskRequest, ctx: RequestContext = Depends(require_context)) -> AskResponse:
    """Answer from the caller's own tenant documents, with citations."""
    log.info("ask tenant=%s user=%s", ctx.tenant_id, ctx.user_id)
    return answer_question(ctx.tenant_id, request.question)


@router.post("/ask/stream")
def ask_stream(request: AskRequest, ctx: RequestContext = Depends(require_context)) -> StreamingResponse:
    """Same as /ask but as server-sent events so the UI can show the answer as it is written."""
    log.info("ask/stream tenant=%s user=%s", ctx.tenant_id, ctx.user_id)
    return StreamingResponse(
        stream_answer(ctx.tenant_id, request.question),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
