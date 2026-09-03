using FlowForge.Application.Admin.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Queries;

public record GetAdminAuditLogsQuery : IRequest<Result<List<AdminAuditLogDto>>>;

public class GetAdminAuditLogsQueryHandler : IRequestHandler<GetAdminAuditLogsQuery, Result<List<AdminAuditLogDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetAdminAuditLogsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AdminAuditLogDto>>> Handle(GetAdminAuditLogsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure<List<AdminAuditLogDto>>(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var logs = (await _uow.AuditLogs.GetRecentAsync(200, ct)).ToList();

        var tenantNames = new Dictionary<Guid, string>();
        var userNames = new Dictionary<Guid, string>();

        var result = new List<AdminAuditLogDto>();
        foreach (var log in logs)
        {
            if (!tenantNames.TryGetValue(log.TenantId, out var tenantName))
            {
                var tenant = await _uow.Tenants.GetByIdAsync(log.TenantId, ct);
                tenantName = tenant?.Name ?? "(deleted tenant)";
                tenantNames[log.TenantId] = tenantName;
            }

            string? userName = null;
            if (log.UserId.HasValue)
            {
                if (!userNames.TryGetValue(log.UserId.Value, out userName))
                {
                    var user = await _uow.Users.GetByIdAsync(log.UserId.Value, ct);
                    userName = user?.FullName ?? "(deleted user)";
                    userNames[log.UserId.Value] = userName;
                }
            }

            result.Add(new AdminAuditLogDto(
                log.Id, log.TenantId, tenantName, log.UserId, userName,
                log.Action, log.EntityType, log.Severity, log.CreatedAt));
        }

        return Result.Success(result);
    }
}
