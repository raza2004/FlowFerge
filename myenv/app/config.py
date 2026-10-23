from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Every setting comes from an environment variable (or a local .env file).

    Nothing secret has a default, so a missing key fails loudly where it is used
    instead of silently talking to the wrong service.
    """

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # --- LLM provider (OpenAI, or any OpenAI compatible API such as OpenRouter) ---
    openai_api_key: str = ""
    openai_base_url: str = ""
    embedding_model: str = "text-embedding-3-small"
    embedding_dimensions: int = 1536
    chat_model: str = "gpt-4o-mini"
    # Comma separated. Tried in order when the main model is rate limited or unavailable.
    chat_fallback_models: str = ""
    rerank_model: str = ""  # empty = use chat_model
    # Estimated price per 1M tokens, used only for the cost numbers in usage logs.
    price_input_per_million: float = 0.15
    price_output_per_million: float = 0.60

    # --- Retrieval ---
    top_k: int = 5
    candidate_k: int = 20  # how many candidates hybrid search hands to the reranker
    chunk_tokens: int = 350
    chunk_overlap_tokens: int = 50
    retrieval_mode: str = "hybrid_rerank"  # vector | hybrid | hybrid_rerank
    # Below this cosine similarity the best chunk is treated as "not relevant".
    min_score: float = 0.0

    # --- Storage ---
    database_url: str = ""  # postgresql://user:pass@host:5432/db (needs the pgvector extension)
    minio_endpoint: str = "localhost:9000"
    minio_access_key: str = ""
    minio_secret_key: str = ""
    minio_bucket: str = "flowforge-files"
    minio_secure: bool = False

    # --- Messaging ---
    rabbitmq_url: str = "amqp://guest:guest@localhost:5672/"
    ingest_queue: str = "ai.documents.ingest"

    # --- Service to service ---
    # The .NET API sends this in X-Internal-Key. Empty disables the check (local dev only).
    internal_api_key: str = ""
    dotnet_api_url: str = "http://localhost:5076"

    # --- Observability ---
    otlp_endpoint: str = ""  # e.g. http://localhost:4317
    service_name: str = "flowforge-ai"


@lru_cache
def get_settings() -> Settings:
    return Settings()
