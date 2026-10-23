import logging

from fastapi import APIRouter, Depends

from app.agent import run_agent
from app.schemas import AgentResponse, AskRequest
from app.security import RequestContext, require_context

router = APIRouter()
log = logging.getLogger(__name__)


@router.post("/agent/run", response_model=AgentResponse)
def agent_run(request: AskRequest, ctx: RequestContext = Depends(require_context)) -> AgentResponse:
    """Run the tool-calling agent. Writes come back as proposed_actions, never as changes."""
    log.info("agent tenant=%s user=%s", ctx.tenant_id, ctx.user_id)
    return run_agent(ctx.tenant_id, request.question)
