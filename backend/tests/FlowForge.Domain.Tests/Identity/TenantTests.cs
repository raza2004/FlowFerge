using FlowForge.Domain.Identity;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Identity;

public class TenantTests
{
    private static Tenant CreateTenant() => Tenant.Create("Acme Inc", "acme", Guid.NewGuid()).Value;

    [Fact]
    public void Create_NormalizesSlugToLowercase()
    {
        var tenant = Tenant.Create("Acme Inc", "ACME-Corp", Guid.NewGuid()).Value;

        tenant.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public void Create_IsActiveByDefault()
    {
        var tenant = CreateTenant();

        tenant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Suspend_SetsInactiveWithReason()
    {
        var tenant = CreateTenant();

        var result = tenant.Suspend("Billing overdue");

        result.IsSuccess.Should().BeTrue();
        tenant.IsActive.Should().BeFalse();
        tenant.SuspensionReason.Should().Be("Billing overdue");
        tenant.SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public void Suspend_WhenAlreadySuspended_Fails()
    {
        var tenant = CreateTenant();
        tenant.Suspend("First reason");

        var result = tenant.Suspend("Second reason");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.AlreadySuspended");
    }

    [Fact]
    public void Reactivate_ClearsSuspensionState()
    {
        var tenant = CreateTenant();
        tenant.Suspend("Billing overdue");

        tenant.Reactivate();

        tenant.IsActive.Should().BeTrue();
        tenant.SuspendedAt.Should().BeNull();
        tenant.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void SetSlackWebhook_WithHttpsUrl_Succeeds()
    {
        var tenant = CreateTenant();

        var result = tenant.SetSlackWebhook("https://hooks.slack.com/services/x/y/z");

        result.IsSuccess.Should().BeTrue();
        tenant.SlackWebhookUrl.Should().Be("https://hooks.slack.com/services/x/y/z");
    }

    [Fact]
    public void SetSlackWebhook_WithNullOrBlank_ClearsIt()
    {
        var tenant = CreateTenant();
        tenant.SetSlackWebhook("https://hooks.slack.com/services/x/y/z");

        var result = tenant.SetSlackWebhook(null);

        result.IsSuccess.Should().BeTrue();
        tenant.SlackWebhookUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("http://hooks.slack.com/services/x")]  // not https
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    public void SetSlackWebhook_WithNonHttpsOrInvalidUrl_Fails(string url)
    {
        var tenant = CreateTenant();

        var result = tenant.SetSlackWebhook(url);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidWebhook");
    }

    [Fact]
    public void CanAddMember_WhenUnderTheLimit_IsTrue()
    {
        var tenant = CreateTenant();

        tenant.CanAddMember().Should().BeTrue();
    }
}
