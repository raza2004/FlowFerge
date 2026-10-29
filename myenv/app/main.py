import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.config import get_settings
from app.llm import LlmUnavailable
from app.observability import CorrelationMiddleware, setup_logging, setup_tracing
from app.routes import agent, ask, documents, health

log = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(_app: FastAPI):
    setup_logging()
    if get_settings().database_url:
        from app.db import init_schema

        init_schema()
    else:
        log.warning("DATABASE_URL is not set: /health works, but search and ingestion will not.")
    yield


app = FastAPI(title="FlowForge AI service", version="0.2.0", lifespan=lifespan)
app.add_middleware(CorrelationMiddleware)
setup_tracing(app)

app.include_router(health.router)
app.include_router(ask.router)
app.include_router(agent.router)
app.include_router(documents.router)


@app.exception_handler(LlmUnavailable)
async def llm_unavailable(_request: Request, ex: LlmUnavailable) -> JSONResponse:
    # 429 = the provider is rate limiting us (retrying right away only makes it worse);
    # 503 = it is down or broken. The .NET client treats the two differently.
    if ex.rate_limited:
        return JSONResponse(status_code=429, content={"detail": "The AI model is busy right now."}, headers={"Retry-After": "30"})
    return JSONResponse(status_code=503, content={"detail": "The AI model is unavailable right now."})


@app.exception_handler(RuntimeError)
async def not_configured(_request: Request, ex: RuntimeError) -> JSONResponse:
    log.error("configuration error: %s", ex)
    return JSONResponse(status_code=503, content={"detail": "The AI service is not configured."})
