using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var startTime = DateTime.UtcNow;
        var response = await next();
        var elapsed = DateTime.UtcNow - startTime;

        _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, elapsed.TotalMilliseconds);
        return response;
    }
}
