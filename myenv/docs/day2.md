# Day 2: connecting the .NET API to the AI service

## The rule
The browser talks only to the .NET API. The API checks who the user is and which workspace they are in, then calls the Python service on their behalf. Python is never reachable from outside.

```
Angular ──JWT──▶ .NET API ──X-Tenant-Id, X-User-Id, X-Correlation-Id, X-Internal-Key──▶ FastAPI ──▶ pgvector + LLM
```

## What was built

| Piece | Where | Job |
|---|---|---|
| `IAiServiceClient` | Application | The one interface the rest of the API uses. Every method returns a `Result`, never throws for an outage |
| `AiServiceClient` | Infrastructure | Typed HttpClient. Maps every failure (timeout, refused, 429, 5xx, open circuit) to a friendly error |
| `AiServiceRegistration` | Infrastructure | Timeout, retries, circuit breaker, connect timeout |
| `AiServiceHeadersHandler` | Infrastructure | Adds tenant, user, correlation id and the shared key to every call |
| `CorrelationIdMiddleware` | API | One id per request, in logs, in the response, forwarded to Python |
| `AssistantController` | API | `POST /api/v1/ai/ask`, `/ai/ask/stream`, and `GET/POST/DELETE /ai/documents` |
| Assistant handlers | Application | Check login, tenant, role and the `ai-assistant` feature flag before calling Python |

## Decisions and the reason for each

- **Gatekeeper.** The tenant id comes from the signed token, never from the request body or headers the caller sends. A test sends a spoofed `X-Tenant-Id` and proves it is ignored.
- **Shared key.** Python rejects any call without `X-Internal-Key`. Calling it directly returns 401.
- **15 second budget, 2 retries with exponential backoff and jitter.** Retries cover quick failures only (network errors and 5xx).
- **No retry on 429.** A rate limited provider recovers only if we back off. A retry adds load and makes it worse. We saw this happen live.
- **No retry on a timeout.** A timed out answer is still running on the AI service; a retry starts a second copy of the same expensive work. We saw three copies pile up live before changing this.
- **Circuit breaker.** After repeated failures the API stops calling the broken service and answers in milliseconds. 429s do not count as failures: busy is not broken.
- **2 second connect timeout.** A stopped service is noticed in about 2 seconds. Without it Windows took about 12 seconds to report "connection refused".
- **Uploads get 120 seconds and no auto retry.** Ingestion embeds every chunk; a duplicate large upload is worse than one clear error. The client picks the document id, so a manual re-upload replaces instead of duplicating.
- **Streaming through the API.** The API copies the AI service's server-sent events to the browser and flushes after every chunk. The 15 second limit applies to the response headers, so the stream itself can run longer.
- **Separate vector database.** The AI service owns `chunks` in its own Postgres (`ai-db`). No shared tables, so neither service can break the other's schema.
- **Handler uses `IHttpContextAccessor`, not `ICurrentUser`.** HttpClient handlers live in their own DI scope, so a scoped service injected there is not the current request's user.

## Bugs the tests and live run caught
1. `MaxRetryAttempts = 0` is invalid in the resilience library. Every upload would have thrown. Fixed with a retry that never decides to run.
2. The rate limiter sat after authorization, so unauthenticated floods were never limited. Moved before authorization.
3. Hidden retries (the OpenAI SDK, the fallback chain and the .NET client) multiplied load on a rate limited provider. SDK retries are now off.
4. The free model fallback list was stale. The free catalog rotates; check `openrouter.ai/models?max_price=0`.

## Verified
- Backend: 93 domain, 69 application, 78 integration tests pass (240).
- Python: 38 tests pass with a pgvector database, including tenant isolation.
- Live, through both services: sign in, upload, answer with citations, "I do not know" for an unrelated question, another workspace sees nothing, streaming, the same correlation id in both logs, a clean 503 when the AI service is stopped while the rest of FlowForge keeps working.

## Not done yet
- Angular chat page and document screen (the API is ready for them).
- Upload goes straight to the AI service. The RabbitMQ and outbox path (async ingestion) is not built.
- The agent exists in Python but is not exposed through the API yet.
- No answer cache, no recall or MRR numbers yet (the golden set is still 18 questions).
- Free models answer in 7 to 17 seconds, so the 15 second limit sometimes trips. `RETRIEVAL_MODE=hybrid` skips the rerank call and saves one model round trip.
