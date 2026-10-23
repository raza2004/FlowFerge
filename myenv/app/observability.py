"""Logging with a correlation id, and optional OpenTelemetry tracing.

The .NET API sends X-Correlation-Id on every call. We put it in a context variable, so every log
line written while handling that request carries it, and the same id appears in the API's logs.
"""

import contextvars
import logging
import uuid

from starlette.middleware.base import BaseHTTPMiddleware

from app.config import get_settings

correlation_id: contextvars.ContextVar[str] = contextvars.ContextVar("correlation_id", default="-")


class _CorrelationFilter(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        record.correlation_id = correlation_id.get()
        return True


def setup_logging() -> None:
    handler = logging.StreamHandler()
    handler.addFilter(_CorrelationFilter())
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s [%(correlation_id)s] %(name)s: %(message)s"))
    root = logging.getLogger()
    root.handlers = [handler]
    root.setLevel(logging.INFO)


class CorrelationMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request, call_next):
        incoming = request.headers.get("x-correlation-id") or str(uuid.uuid4())
        token = correlation_id.set(incoming)
        try:
            response = await call_next(request)
        finally:
            correlation_id.reset(token)
        response.headers["X-Correlation-Id"] = incoming
        return response


def setup_tracing(app) -> None:
    """Export traces over OTLP when OTLP_ENDPOINT is set. The incoming `traceparent` header from
    the .NET API is honoured, so one trace spans both services."""
    settings = get_settings()
    if not settings.otlp_endpoint:
        return
    from opentelemetry import trace
    from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
    from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
    from opentelemetry.sdk.resources import Resource
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor

    provider = TracerProvider(resource=Resource.create({"service.name": settings.service_name}))
    provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter(endpoint=settings.otlp_endpoint, insecure=True)))
    trace.set_tracer_provider(provider)
    FastAPIInstrumentor.instrument_app(app)
