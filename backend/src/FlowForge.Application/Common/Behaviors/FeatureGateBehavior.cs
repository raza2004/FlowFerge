using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Features;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Common.Behaviors;

/// <summary>
/// Blocks any request tagged with IRequiresFeature while that feature is switched off for the
/// caller's workspace. Doing it here means no handler has to remember to check, and a new gated
/// request needs only the interface.
/// </summary>
public class FeatureGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IFeatureGate _gate;
    private readonly ICurrentUser _currentUser;

    public FeatureGateBehavior(IFeatureGate gate, ICurrentUser currentUser)
    {
        _gate = gate;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IRequiresFeature gated || _currentUser.TenantId == null)
            return await next();

        if (await _gate.IsEnabledAsync(gated.FeatureKey, _currentUser.TenantId.Value, ct))
            return await next();

        var feature = FeatureKeys.All.FirstOrDefault(f => f.Key == gated.FeatureKey)?.Name ?? gated.FeatureKey;
        var error = Error.Forbidden("Feature.Disabled", $"{feature} is turned off for this workspace.");

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failure = typeof(Result).GetMethods()
                .First(m => m.Name == "Failure" && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);
            return (TResponse)failure.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException($"{typeof(TRequest).Name} requires a feature but doesn't return a Result.");
    }
}
