using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Jobs;

public record CleanupResult(int RefreshTokensDeleted, int InvitationsDeleted);

/// <summary>
/// Housekeeping for rows that only ever pile up: sign-in tokens that expired or were revoked, and
/// invitations that were used, revoked or expired. A grace period is kept before deleting, so recent
/// history is still around for troubleshooting ("why couldn't I sign in last week?").
/// </summary>
public class DataCleanupService
{
    public static readonly TimeSpan TokenRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan InvitationRetention = TimeSpan.FromDays(90);

    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DataCleanupService> _logger;

    public DataCleanupService(IUnitOfWork uow, IDateTimeProvider clock, ILogger<DataCleanupService> logger)
    {
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CleanupResult> RunAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var tokens = await _uow.RefreshTokens.DeleteInactiveAsync(now - TokenRetention, ct);
        var invitations = await _uow.Invitations.DeleteClosedAsync(now - InvitationRetention, ct);

        _logger.LogInformation("Cleanup removed {Tokens} old refresh tokens and {Invitations} old invitations", tokens, invitations);
        return new CleanupResult(tokens, invitations);
    }
}
