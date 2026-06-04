namespace FlowForge.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
    bool IsSystemAdmin { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
