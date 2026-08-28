using System.Text.Json;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace FlowForge.Infrastructure.Services.AI;

/// <summary>
/// IAiService backed by the OpenAI SDK. Every public method fails gracefully (via Result)
/// rather than throwing when there's no API key configured, the request errors out, or
/// the model's response can't be parsed - AI is a feature the app degrades without, not
/// a hard dependency the app crashes without.
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
        var clientResult = TryCreateClient();
        if (clientResult.IsFailure) return Result.Failure<SubtaskSuggestion>(clientResult.Error);

        var userPrompt =
            $"Task: {taskTitle}\n" +
            $"Description: {(string.IsNullOrWhiteSpace(taskDescription) ? "(none)" : taskDescription)}\n\n" +
            "Break this task into 3 to 6 concrete, actionable subtasks a developer could pick up " +
            "individually. Respond with JSON only, in exactly this shape: " +
            "{\"subtasks\": [\"...\", \"...\"]}";

        var jsonResult = await CompleteJsonAsync(clientResult.Value,
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
        var clientResult = TryCreateClient();
        if (clientResult.IsFailure) return Result.Failure<AssigneeSuggestion>(clientResult.Error);

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

        var jsonResult = await CompleteJsonAsync(clientResult.Value,
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
        var clientResult = TryCreateClient();
        if (clientResult.IsFailure) return Result.Failure<string>(clientResult.Error);

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

        try
        {
            var options = new ChatCompletionOptions();
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a concise, direct engineering manager writing a project status update."),
                new UserChatMessage(userPrompt)
            };

            var completion = await clientResult.Value.CompleteChatAsync(messages, options, ct);
            var text = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : null;

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<string>(Error.Failure("AI.EmptyResponse", "The AI did not return a summary"));

            return Result.Success(text.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI request failed while summarizing project {ProjectName}", input.ProjectName);
            return Result.Failure<string>(Error.Failure("AI.RequestFailed", "The AI request failed"));
        }
    }

    private Result<ChatClient> TryCreateClient()
    {
        var apiKey = _config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Result.Failure<ChatClient>(Error.Failure("AI.NotConfigured",
                "OpenAI API key is not configured. Set OpenAI:ApiKey in appsettings (or the OpenAI__ApiKey environment variable) to enable AI features."));
        }

        var model = _config["OpenAI:Model"];
        model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model;

        return Result.Success(new ChatClient(model, apiKey));
    }

    private async Task<Result<string>> CompleteJsonAsync(ChatClient client, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        try
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages, options, ct);
            var text = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : null;

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<string>(Error.Failure("AI.EmptyResponse", "The AI returned an empty response"));

            return Result.Success(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI request failed");
            return Result.Failure<string>(Error.Failure("AI.RequestFailed", "The AI request failed"));
        }
    }
}
