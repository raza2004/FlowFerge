using FlowForge.Domain.Features;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Features;

public class FeatureFlagTests
{
    private static FeatureFlag Flag(bool enabled) => FeatureFlag.Create("Some-Feature", "Some feature", "desc", enabled).Value;

    [Fact]
    public void Key_IsNormalizedToLowerCase()
    {
        Flag(true).Key.Should().Be("some-feature");
    }

    [Fact]
    public void WithoutAnOverride_TheGlobalSettingApplies()
    {
        Flag(true).IsEnabledFor(Guid.NewGuid()).Should().BeTrue();
        Flag(false).IsEnabledFor(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void AnOverride_BeatsTheGlobalSetting_ForThatWorkspaceOnly()
    {
        var flag = Flag(true);
        var blocked = Guid.NewGuid();
        var other = Guid.NewGuid();

        flag.SetOverride(blocked, false);

        flag.IsEnabledFor(blocked).Should().BeFalse();
        flag.IsEnabledFor(other).Should().BeTrue();

        var off = Flag(false);
        off.SetOverride(blocked, true);
        off.IsEnabledFor(blocked).Should().BeTrue();
        off.IsEnabledFor(other).Should().BeFalse();
    }

    [Fact]
    public void SettingAnOverrideTwice_UpdatesInsteadOfDuplicating_AndRemovingRestoresTheGlobal()
    {
        var flag = Flag(true);
        var tenant = Guid.NewGuid();

        flag.SetOverride(tenant, false);
        flag.SetOverride(tenant, true);
        flag.Overrides.Should().ContainSingle();

        flag.SetOverride(tenant, false);
        flag.RemoveOverride(tenant);
        flag.Overrides.Should().BeEmpty();
        flag.IsEnabledFor(tenant).Should().BeTrue();
    }

    [Fact]
    public void Create_RejectsAnEmptyKey()
    {
        FeatureFlag.Create(" ", "n", "d", true).IsFailure.Should().BeTrue();
    }
}
