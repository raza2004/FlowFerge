using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.ValueObjects;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Identity;

public class UserTests
{
    private static User CreateUser() =>
        User.Create(Email.Create("jane@example.com").Value, "Jane", "Doe", "hashed-password").Value;

    [Fact]
    public void Create_DefaultsEmailNotificationsToEnabled()
    {
        var user = CreateUser();

        user.EmailNotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_StartsAsPendingWithAVerificationToken()
    {
        var user = CreateUser();

        user.Status.Should().Be(UserStatus.Pending);
        user.IsEmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var user = CreateUser();

        user.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public void SetEmailNotificationsEnabled_CanBeToggledOff()
    {
        var user = CreateUser();

        user.SetEmailNotificationsEnabled(false);

        user.EmailNotificationsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_LocksAccountAfterFiveAttempts()
    {
        var user = CreateUser();

        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin();
            user.IsLocked.Should().BeFalse();
        }

        user.RecordFailedLogin();

        user.IsLocked.Should().BeTrue();
        user.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public void RecordSuccessfulLogin_ClearsLockoutState()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();
        user.IsLocked.Should().BeTrue();

        user.RecordSuccessfulLogin();

        user.IsLocked.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public void VerifyEmail_WithCorrectToken_ActivatesTheAccount()
    {
        var user = CreateUser();
        var token = user.EmailVerificationToken!;

        var result = user.VerifyEmail(token);

        result.IsSuccess.Should().BeTrue();
        user.IsEmailVerified.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void VerifyEmail_WithWrongToken_Fails()
    {
        var user = CreateUser();

        var result = user.VerifyEmail("wrong-token");

        result.IsFailure.Should().BeTrue();
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void Suspend_ThenReactivate_TogglesStatus()
    {
        var user = CreateUser();

        user.Suspend();
        user.Status.Should().Be(UserStatus.Suspended);

        user.Reactivate();
        user.Status.Should().Be(UserStatus.Active);
    }
}
