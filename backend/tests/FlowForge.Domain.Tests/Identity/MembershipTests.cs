using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Identity;

public class MembershipTests
{
    [Fact]
    public void Create_WithoutAnInviter_IsImmediatelyAccepted()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Owner).Value;

        membership.IsActive.Should().BeTrue();
        membership.InvitationToken.Should().BeNull();
        membership.InvitationAcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithAnInviter_IssuesAPendingInvitationToken()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member, invitedById: Guid.NewGuid()).Value;

        membership.InvitationToken.Should().NotBeNullOrEmpty();
        membership.InvitationAcceptedAt.Should().BeNull();
    }

    [Fact]
    public void AcceptInvitation_WithTheCorrectToken_Succeeds()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member, invitedById: Guid.NewGuid()).Value;
        var token = membership.InvitationToken!;

        var result = membership.AcceptInvitation(token);

        result.IsSuccess.Should().BeTrue();
        membership.InvitationToken.Should().BeNull();
        membership.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AcceptInvitation_WithTheWrongToken_Fails()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member, invitedById: Guid.NewGuid()).Value;

        var result = membership.AcceptInvitation("wrong-token");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ChangeRole_CannotDemoteTheOwner()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Owner).Value;

        var result = membership.ChangeRole(MembershipRole.Member, MembershipRole.Owner);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Membership.CannotDemoteOwner");
        membership.Role.Should().Be(MembershipRole.Owner);
    }

    [Fact]
    public void ChangeRole_ByAMemberRole_IsForbidden()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member).Value;

        var result = membership.ChangeRole(MembershipRole.Manager, changedByRole: MembershipRole.Member);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Membership.InsufficientPermission");
    }

    [Fact]
    public void ChangeRole_ByAnAdmin_Succeeds()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member).Value;

        var result = membership.ChangeRole(MembershipRole.Manager, changedByRole: MembershipRole.Admin);

        result.IsSuccess.Should().BeTrue();
        membership.Role.Should().Be(MembershipRole.Manager);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndRecordsLeftAt()
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), MembershipRole.Member).Value;

        membership.Deactivate();

        membership.IsActive.Should().BeFalse();
        membership.LeftAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(MembershipRole.Owner, MembershipRole.Admin, true)]
    [InlineData(MembershipRole.Member, MembershipRole.Admin, false)]
    [InlineData(MembershipRole.Member, MembershipRole.Member, true)]
    public void HasPermission_ComparesRoleRankNumerically(MembershipRole actual, MembershipRole minimumRequired, bool expected)
    {
        var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), actual).Value;

        membership.HasPermission(minimumRequired).Should().Be(expected);
    }
}
