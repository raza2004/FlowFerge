using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.ValueObjects;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Identity;

public class InvitationTests
{
    private static Invitation NewInvitation(int daysValid = Invitation.DefaultDaysValid) =>
        Invitation.Create(Guid.NewGuid(), Email.Create("invitee@example.com").Value, MembershipRole.Member, Guid.NewGuid(), daysValid).Value;

    [Fact]
    public void Create_IsPendingWithAUniqueUrlSafeToken()
    {
        var a = NewInvitation();
        var b = NewInvitation();

        a.IsPending.Should().BeTrue();
        a.Token.Should().NotBe(b.Token);
        a.Token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void Create_RejectsOwnerRole()
    {
        var result = Invitation.Create(Guid.NewGuid(), Email.Create("x@example.com").Value, MembershipRole.Owner, Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Accept_CanOnlyHappenOnce()
    {
        var invitation = NewInvitation();
        var userId = Guid.NewGuid();

        invitation.Accept(userId).IsSuccess.Should().BeTrue();
        invitation.AcceptedByUserId.Should().Be(userId);
        invitation.IsPending.Should().BeFalse();

        invitation.Accept(Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Accept_FailsOnceRevoked()
    {
        var invitation = NewInvitation();
        invitation.Revoke();

        invitation.IsPending.Should().BeFalse();
        invitation.Accept(Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Accept_FailsOnceExpired()
    {
        var invitation = NewInvitation(daysValid: 0);

        invitation.IsExpired.Should().BeTrue();
        invitation.Accept(Guid.NewGuid()).IsFailure.Should().BeTrue();
    }
}
