using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Identity.Commands;

/// <summary>
/// Invites an email address into the current workspace. Re-inviting the same address replaces
/// the earlier pending invite (so "resend" is just inviting again). The email is best-effort:
/// the invite link is also returned so an admin can share it directly if mail isn't set up.
/// </summary>
public record CreateInvitationCommand(string Email, string Role) : IRequest<Result<InvitationDto>>;

public class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).NotEmpty();
    }
}

public class CreateInvitationCommandHandler : IRequestHandler<CreateInvitationCommand, Result<InvitationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailSender _email;
    private readonly IAppLinks _links;
    private readonly ILogger<CreateInvitationCommandHandler> _logger;

    public CreateInvitationCommandHandler(
        IUnitOfWork uow, ICurrentUser currentUser, IEmailSender email, IAppLinks links,
        ILogger<CreateInvitationCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _email = email;
        _links = links;
        _logger = logger;
    }

    public async Task<Result<InvitationDto>> Handle(CreateInvitationCommand request, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(_uow, _currentUser, ct);
        if (access.IsFailure) return Result.Failure<InvitationDto>(access.Error);
        var inviterMembership = access.Value;

        var roleResult = WorkspaceAccess.ParseAssignableRole(request.Role);
        if (roleResult.IsFailure) return Result.Failure<InvitationDto>(roleResult.Error);

        // You can't hand out more access than you have yourself (lower enum value = more access).
        if (roleResult.Value < inviterMembership.Role)
            return Result.Failure<InvitationDto>(Error.Forbidden("Invitation.RoleTooHigh", "You can't invite someone with a higher role than your own"));

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure) return Result.Failure<InvitationDto>(emailResult.Error);
        var email = emailResult.Value;
        var tenantId = inviterMembership.TenantId;

        var existingUser = await _uow.Users.GetByEmailAsync(email, ct);
        if (existingUser != null)
        {
            var existingMembership = await _uow.Memberships.GetByUserAndTenantAsync(existingUser.Id, tenantId, ct);
            if (existingMembership is { IsActive: true })
                return Result.Failure<InvitationDto>(Error.Conflict("Invitation.AlreadyMember", "That person is already in this workspace"));
        }

        foreach (var previous in await _uow.Invitations.GetPendingByTenantAndEmailAsync(tenantId, email, ct))
            previous.Revoke();

        var invitationResult = Invitation.Create(tenantId, email, roleResult.Value, inviterMembership.UserId);
        if (invitationResult.IsFailure) return Result.Failure<InvitationDto>(invitationResult.Error);

        var invitation = invitationResult.Value;
        await _uow.Invitations.AddAsync(invitation, ct);
        await _uow.SaveChangesAsync(ct);

        var inviter = await _uow.Users.GetByIdAsync(inviterMembership.UserId, ct);
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId, ct);
        var inviterName = inviter?.FullName ?? "A teammate";
        var url = _links.InvitationUrl(invitation.Token);

        var sendResult = await _email.SendAsync(
            email.Value,
            $"{inviterName} invited you to {tenant?.Name ?? "a workspace"} on FlowForge",
            $"<p><strong>{System.Net.WebUtility.HtmlEncode(inviterName)}</strong> invited you to join " +
            $"<strong>{System.Net.WebUtility.HtmlEncode(tenant?.Name ?? "their workspace")}</strong> on FlowForge " +
            $"as a {invitation.Role}.</p>" +
            $"<p><a href=\"{url}\">Accept the invitation</a></p>" +
            $"<p style=\"color:#888;font-size:12px\">This link expires on {invitation.ExpiresAt:MMMM d, yyyy}.</p>",
            ct);
        if (sendResult.IsFailure)
            _logger.LogWarning("Invitation email to {Email} failed: {Error}", email.Value, sendResult.Error.Message);

        return Result.Success(new InvitationDto(
            invitation.Id, email.Value, invitation.Role.ToString(), inviterName,
            invitation.CreatedAt, invitation.ExpiresAt, url));
    }
}
