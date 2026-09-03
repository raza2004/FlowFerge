using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Auditing;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Common.Behaviors;

/// <summary>
/// Writes an AuditLog row for every successful *Command* (by naming convention, matching
/// how this codebase already distinguishes Commands from Queries everywhere else). Queries
/// are never logged - reads aren't audit-worthy and would drown the log. This is what
/// makes "Admin Panel -> audit logs across all tenants" real instead of an empty table:
/// every mutation gets logged automatically, with no per-handler wiring required.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(IUnitOfWork uow, ICurrentUser currentUser, ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();

        var requestName = typeof(TRequest).Name;
        var isSuccess = response is not Result result || result.IsSuccess;

        if (!requestName.EndsWith("Command", StringComparison.Ordinal) || !isSuccess || _currentUser.TenantId == null)
            return response;

        try
        {
            var entry = AuditLog.Create(
                _currentUser.TenantId.Value,
                _currentUser.UserId,
                action: requestName,
                entityType: InferEntityType(typeof(TRequest)),
                ipAddress: _currentUser.IpAddress,
                userAgent: _currentUser.UserAgent);

            await _uow.AuditLogs.AddAsync(entry, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging must never be why a request fails - the command already succeeded.
            _logger.LogWarning(ex, "Failed to write audit log for {RequestName}", requestName);
        }

        return response;
    }

    /// <summary>
    /// Buckets by bounded context from the command's namespace, e.g.
    /// "FlowForge.Application.Projects.Commands" -> "Projects". Generic on purpose so new
    /// commands are audited automatically without ever touching this file again.
    /// </summary>
    private static string InferEntityType(Type requestType)
    {
        var ns = requestType.Namespace ?? string.Empty;
        var parts = ns.Split('.');
        var index = Array.IndexOf(parts, "Application");
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : "Unknown";
    }
}
