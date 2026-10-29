namespace FlowForge.Infrastructure.Services.AI;

/// <summary>Bound from the "AiService" configuration section.</summary>
public class AiServiceOptions
{
    public const string Section = "AiService";

    /// <summary>Where the Python service listens, e.g. http://localhost:8001 (or http://ai-service:8000 in Docker).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Shared secret sent as X-Internal-Key. The AI service rejects calls without it.</summary>
    public string InternalKey { get; set; } = "";

    /// <summary>Upper bound for a question, including retries. The UI gets an answer or a friendly error within this.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    /// <summary>Document ingestion embeds every chunk, so it gets a much longer limit and no retries.</summary>
    public int IngestTimeoutSeconds { get; set; } = 120;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
