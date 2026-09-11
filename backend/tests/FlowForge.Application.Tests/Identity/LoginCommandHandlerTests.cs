using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.Commands;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Identity.ValueObjects;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Identity;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGenerator = new();
    private readonly User _user;

    public LoginCommandHandlerTests()
    {
        _user = User.Create(Email.Create("jane@example.com").Value, "Jane", "Doe", "hashed").Value;

        var usersRepo = new Mock<IUserRepository>();
        usersRepo.Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _uow.SetupGet(x => x.Users).Returns(usersRepo.Object);

        _uow.SetupGet(x => x.RefreshTokens).Returns(Mock.Of<IRefreshTokenRepository>());
        _uow.SetupGet(x => x.Memberships).Returns(Mock.Of<IMembershipRepository>());
        Mock.Get(_uow.Object.Memberships)
            .Setup(x => x.GetByUserAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Membership>());

        _passwordHasher.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _jwtGenerator.Setup(x => x.GenerateAccessTokenWithExpiry(It.IsAny<User>(), It.IsAny<Membership?>()))
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
    }

    private LoginCommandHandler CreateHandler() => new(_uow.Object, _passwordHasher.Object, _jwtGenerator.Object);

    private static LoginCommand ValidLoginCommand() => new("jane@example.com", "correct-password", null, null);

    [Fact]
    public async Task Handle_WithASuspendedAccount_FailsWithoutCheckingThePassword()
    {
        // Regression test: LoginCommand used to check IsLocked but never Status, so
        // AdminController's "suspend user" action suspended nothing in practice - a
        // suspended user could still log in normally.
        _user.Suspend();

        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.AccountSuspended");
        _passwordHasher.Verify(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithALockedAccount_Fails()
    {
        for (var i = 0; i < 5; i++) _user.RecordFailedLogin();

        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.AccountLocked");
    }

    [Fact]
    public async Task Handle_WithTheWrongPassword_FailsAndRecordsTheFailedAttempt()
    {
        _passwordHasher.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        _user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoMemberships_SucceedsWithNoActiveTenant()
    {
        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tenant.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTheOnlyMembershipsTenantIsSuspended_SucceedsWithNoActiveTenant()
    {
        // Regression test: the primary-membership selection used to pick the first
        // active membership regardless of the tenant's own IsActive flag, so a user
        // whose workspace got suspended would still get a token scoped to it.
        var tenant = Tenant.Create("Acme", "acme", Guid.NewGuid()).Value;
        tenant.Suspend("Billing overdue");
        var membership = Membership.Create(_user.Id, tenant.Id, MembershipRole.Owner).Value;

        Mock.Get(_uow.Object.Memberships)
            .Setup(x => x.GetByUserAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { membership });
        var tenantsRepo = new Mock<ITenantRepository>();
        tenantsRepo.Setup(x => x.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _uow.SetupGet(x => x.Tenants).Returns(tenantsRepo.Object);

        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tenant.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAnActiveTenantMembership_SucceedsWithThatTenant()
    {
        var tenant = Tenant.Create("Acme", "acme", Guid.NewGuid()).Value;
        var membership = Membership.Create(_user.Id, tenant.Id, MembershipRole.Owner).Value;

        Mock.Get(_uow.Object.Memberships)
            .Setup(x => x.GetByUserAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { membership });
        var tenantsRepo = new Mock<ITenantRepository>();
        tenantsRepo.Setup(x => x.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _uow.SetupGet(x => x.Tenants).Returns(tenantsRepo.Object);

        var result = await CreateHandler().Handle(ValidLoginCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tenant.Should().NotBeNull();
        result.Value.Tenant!.Id.Should().Be(tenant.Id);
    }
}
