using FlowForge.Shared.Results;

namespace FlowForge.Application.Common.Abstractions;

public interface ISlackNotifier
{
    Task<Result> PostAsync(string webhookUrl, string text, CancellationToken ct = default);
}
