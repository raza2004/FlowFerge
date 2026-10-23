"""Who is calling. The .NET API authenticates the user, then tells this service which tenant the
request belongs to. This service trusts that header only because it also checks a shared secret,
and it is never exposed to the browser."""

import hmac
import uuid
from dataclasses import dataclass

from fastapi import Header, HTTPException

from app.config import get_settings


@dataclass(frozen=True)
class RequestContext:
    tenant_id: uuid.UUID
    user_id: str
    correlation_id: str


def require_context(
    x_tenant_id: str | None = Header(default=None),
    x_user_id: str = Header(default=""),
    x_correlation_id: str = Header(default=""),
    x_internal_key: str = Header(default=""),
) -> RequestContext:
    expected = get_settings().internal_api_key
    if expected and not hmac.compare_digest(x_internal_key, expected):
        raise HTTPException(status_code=401, detail="Invalid internal key")

    try:
        tenant = uuid.UUID(x_tenant_id or "")
    except ValueError:
        raise HTTPException(status_code=400, detail="X-Tenant-Id header is required and must be a UUID")

    return RequestContext(tenant_id=tenant, user_id=x_user_id, correlation_id=x_correlation_id or str(uuid.uuid4()))
