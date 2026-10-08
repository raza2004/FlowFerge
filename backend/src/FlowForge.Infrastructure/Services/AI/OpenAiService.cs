using System.ClientModel;
using System.Text.Json;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace FlowForge.Infrastructure.Services.AI;

/// <summary>
/// IAiService backed by the official OpenAI .NET SDK, which also happens to work against
/// any OpenAI-compatible Chat Completions API - so this same client talks to real OpenAI,
/// OpenRouter, Groq, or Google's OpenAI-compatible Gemini endpoint depending on what
/// OpenAI:BaseUrl / OpenAI:ApiKey / OpenAI:Model are set to. Every public method fails
/// gracefully (via Result) rather than throwing when there's no API key configured, the
/// request errors out, or the model's response can't be parsed - AI is a feature the app
/// degrades without, not a hard dependency the app crashes without.
/// </summary>
public class OpenAiService : IAiService
{
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(IConfiguration config, ILogger<OpenAiService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<Result<SubtaskSuggestion>> SuggestSubtasksAsync(string taskTitle, string? taskDescription, CancellationToken ct = default)
    {
        var configured = EnsureConfigured();
        if (configured.IsFailure) return Result.Failure<SubtaskSuggestion>(configured.Error);

        var userPrompt =
            $"Task: {taskTitle}\n" +
            $"Description: {(string.IsNullOrWhiteSpace(taskDescription) ? "(none)" : taskDescription)}\n\n" +
            "Break this task into 3 to 6 concrete, actionable subtasks a developer could pick up " +
            "individually. Respond with JSON only, in exactly this shape: " +
            "{\"subtasks\": [\"...\", \"...\"]}";

        var jsonResult = await CompleteJsonAsync(
            "You are a precise software project planning assistant. Always respond with valid JSON only, no markdown, no commentary.",
            userPrompt, ct);
        if (jsonResult.IsFailure) return Result.Failure<SubtaskSuggestion>(jsonResult.Error);

        try
        {
            using var doc = JsonDocument.Parse(jsonResult.Value);
            var titles = doc.RootElement.GetProperty("subtasks")
                .EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (titles.Count == 0)
                return Result.Failure<SubtaskSuggestion>(Error.Failure("AI.EmptyResponse", "The AI did not return any subtasks"));

            return Result.Success(new SubtaskSuggestion(titles));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI subtask breakdown response: {Json}", jsonResult.Value);
            return Result.Failure<SubtaskSuggestion>(Error.Failure("AI.ParseError", "Could not parse the AI's response"));
        }
    }

    public async Task<Result<AssigneeSuggestion>> SuggestAssigneeAsync(
        string taskTitle, string? taskDescription, List<AssigneeCandidate> candidates, CancellationToken ct = default)
    {
        var configured = EnsureConfigured();
        if (configured.IsFailure) return Result.Failure<AssigneeSuggestion>(configured.Error);

        var candidateLines = string.Join("\n",
            candidates.Select(c => $"- id: {c.UserId}, name: {c.FullName}, currently has {c.OpenTaskCount} open task(s)"));

        var userPrompt =
            $"Task: {taskTitle}\n" +
            $"Description: {(string.IsNullOrWhiteSpace(taskDescription) ? "(none)" : taskDescription)}\n\n" +
            $"Candidates:\n{candidateLines}\n\n" +
            "Pick the single best candidate for this task, favoring whoever currently has the lightest " +
            "workload unless the task clearly needs a specific skillset the description implies. " +
            "Respond with JSON only, in exactly this shape: " +
            "{\"suggestedUserId\": \"<one of the candidate ids above, verbatim>\", \"reasoning\": \"<one sentence>\"}";

        var jsonResult = await CompleteJsonAsync(
            "You are a precise engineering team lead assistant. Always respond with valid JSON only, no markdown, no commentary.",
            userPrompt, ct);
        if (jsonResult.IsFailure) return Result.Failure<AssigneeSuggestion>(jsonResult.Error);

        try
        {
            using var doc = JsonDocument.Parse(jsonResult.Value);
            var idText = doc.RootElement.GetProperty("suggestedUserId").GetString();
            var reasoning = doc.RootElement.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;

            if (!Guid.TryParse(idText, out var userId))
                return Result.Failure<AssigneeSuggestion>(Error.Failure("AI.ParseError", "The AI's suggested user id was not valid"));

            return Result.Success(new AssigneeSuggestion(userId, reasoning));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI assignee suggestion response: {Json}", jsonResult.Value);
            return Result.Failure<AssigneeSuggestion>(Error.Failure("AI.ParseError", "Could not parse the AI's response"));
        }
    }

    public async Task<Result<string>> SummarizeProjectAsync(ProjectSummaryInput input, CancellationToken ct = default)
    {
        var configured = EnsureConfigured();
        if (configured.IsFailure) return Result.Failure<string>(configured.Error);

        var userPrompt =
            $"Project: {input.ProjectName}\n" +
            $"Total tasks: {input.TotalTasks}\n" +
            $"Completed: {input.CompletedTasks}\n" +
            $"Overdue: {input.OverdueTasks}\n" +
            $"In-progress task titles: {(input.InProgressTaskTitles.Count > 0 ? string.Join(", ", input.InProgressTaskTitles) : "(none)")}\n" +
            $"Overdue task titles: {(input.OverdueTaskTitles.Count > 0 ? string.Join(", ", input.OverdueTaskTitles) : "(none)")}\n\n" +
            "Write a short (3-5 sentence) status update for this project's team, in plain prose, no " +
            "markdown or bullet points. Mention overall progress, call out anything overdue or at risk " +
            "by name, and end with one concrete recommendation.";

        var chat = await ChatAsync(
            new List<ChatMessage>
            {
                new SystemChatMessage("You are a concise, direct engineering manager writing a project status update."),
                new UserChatMessage(userPrompt)
            },
            new ChatCompletionOptions(), ct);

        return chat.IsFailure ? chat : Result.Success(chat.Value.Trim());
    }

    public async Task<Result<BlockerAnalysis>> AnalyzeBlockersAsync(BlockerAnalysisInput input, CancellationToken ct = default)
    {
        var configured = EnsureConfigured();
        if (configured.IsFailure) return Result.Failure<BlockerAnalysis>(configured.Error);

        var userPrompt =
            $"Project: {input.ProjectName}\n" +
            $"Open tasks: {input.OpenTasks}\n\n" +
            "Risks already detected by automated rules:\n" +
            string.Join("\n", input.SignalLines.Select(l => $"- {l}")) + "\n\n" +
            "Do not invent risks beyond this list. Write a 2-3 sentence summary of how serious the situation is " +
            "and which items matter most, then give 2 to 4 specific, actionable recommendations that reference " +
            "tasks or people from the list. Respond with JSON only, in exactly this shape: " +
            "{\"summary\": \"...\", \"recommendations\": [\"...\", \"...\"]}";

        var jsonResult = await CompleteJsonAsync(
            "You are an experienced delivery manager reviewing project risks. Always respond with valid JSON only, no markdown, no commentary.",
            userPrompt, ct);
        if (jsonResult.IsFailure) return Result.Failure<BlockerAnalysis>(jsonResult.Error);

        try
        {
            using var doc = JsonDocument.Parse(jsonResult.Value);
            var summary = doc.RootElement.GetProperty("summary").GetString();
            var recommendations = doc.RootElement.TryGetProperty("recommendations", out var recs)
                ? recs.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                : new List<string>();

            if (string.IsNullOrWhiteSpace(summary))
                return Result.Failure<BlockerAnalysis>(Error.Failure("AI.EmptyResponse", "The AI did not return an analysis"));

            return Result.Success(new BlockerAnalysis(summary.Trim(), recommendations));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI blocker analysis response: {Json}", jsonResult.Value);
            return Result.Failure<BlockerAnalysis>(Error.Failure("AI.ParseError", "Could not parse the AI's response"));
        }
    }

    private Result EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_config["OpenAI:ApiKey"]))
        {
            return Result.Failure(Error.Failure("AI.NotConfigured",
                "AI API key is not configured. Set OpenAI:ApiKey (and, for a non-OpenAI provider, OpenAI:BaseUrl) in appsettings or user-secrets to enable AI features."));
        }

        return Result.Success();
    }

    /// <summary>
    /// The primary model first, then each model in OpenAI:FallbackModels (comma separated). Free
    /// models on a shared provider hit their quotas often, so a busy model quietly hands over to the next.
    /// </summary>
    private List<string> ModelChain()
    {
        var primary = _config["OpenAI:Model"];
        var chain = new List<string> { string.IsNullOrWhiteSpace(primary) ? "gpt-4o-mini" : primary.Trim() };

        var fallbacks = _config["OpenAI:FallbackModels"];
        if (!string.IsNullOrWhiteSpace(fallbacks))
            chain.AddRange(fallbacks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return chain.Distinct().ToList();
    }

    private ChatClient CreateClient(string model)
    {
        var apiKey = _config["OpenAI:ApiKey"]!;
        var baseUrl = _config["OpenAI:BaseUrl"];

        return string.IsNullOrWhiteSpace(baseUrl)
            ? new ChatClient(model, apiKey)
            : new ChatClient(model, new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
    }

    // Statuses where trying a different model can help: rate limited, model gone or not free,
    // provider hiccup, or the model rejecting a request option (e.g. JSON mode) another one accepts.
    private static readonly HashSet<int> TryNextModelStatuses = new() { 400, 402, 404, 408, 429, 500, 502, 503, 504 };

    private async Task<Result<string>> ChatAsync(List<ChatMessage> messages, ChatCompletionOptions options, CancellationToken ct)
    {
        var sawRateLimit = false;

        foreach (var model in ModelChain())
        {
            try
            {
                var completion = await CreateClient(model).CompleteChatAsync(messages, options, ct);
                var text = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : null;

                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("AI model {Model} returned an empty response, trying the next one", model);
                    continue;
                }

                return Result.Success(text);
            }
            catch (ClientResultException ex) when (TryNextModelStatuses.Contains(ex.Status))
            {
                sawRateLimit |= ex.Status == 429;
                _logger.LogWarning("AI model {Model} unavailable (HTTP {Status}), trying the next one", model, ex.Status);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AI request to model {Model} failed", model);
                return Result.Failure<string>(Error.Failure("AI.RequestFailed", "The AI request failed"));
            }
        }

        return sawRateLimit
            ? Result.Failure<string>(Error.TooManyRequests("AI.RateLimited",
                "The AI provider is busy (its free usage limit was reached). Wait a minute and try again, or add more fallback models."))
            : Result.Failure<string>(Error.Failure("AI.RequestFailed", "No AI model could answer right now."));
    }

    private Task<Result<string>> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
        ChatAsync(
            new List<ChatMessage> { new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt) },
            new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() },
            ct);
}
