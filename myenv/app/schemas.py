from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str = "ok"
    service: str = "flowforge-ai"


class AskRequest(BaseModel):
    question: str = Field(min_length=1, max_length=2000)


class Citation(BaseModel):
    """A piece of a document that supports the answer, so a user can check it."""

    document_id: str
    document_name: str
    page: int
    content: str
    score: float


class UsageInfo(BaseModel):
    model: str = ""
    prompt_tokens: int = 0
    completion_tokens: int = 0
    cost_usd: float = 0.0
    latency_ms: int = 0


class AskResponse(BaseModel):
    answer: str
    sources: list[Citation] = Field(default_factory=list)
    usage: UsageInfo = Field(default_factory=UsageInfo)
    retrieval_mode: str = ""


class ToolCallRecord(BaseModel):
    """One row of the agent audit trail (the .NET API stores it with tenant and user)."""

    tool: str
    arguments: dict
    status: str  # ok | error | pending_approval | rejected_arguments
    result_summary: str = ""
    duration_ms: int = 0


class ProposedAction(BaseModel):
    """A write the agent wants to do. It is never executed here; a human approves it in the app."""

    tool: str
    arguments: dict
    summary: str


class AgentResponse(BaseModel):
    answer: str
    tool_calls: list[ToolCallRecord] = Field(default_factory=list)
    proposed_actions: list[ProposedAction] = Field(default_factory=list)
    usage: UsageInfo = Field(default_factory=UsageInfo)
