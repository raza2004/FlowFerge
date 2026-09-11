using FlowForge.Domain.Workflows;
using FlowForge.Domain.Workflows.Enums;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Workflows;

public class AutomationRuleTests
{
    private static TestIds Valid() => new(
        TenantId: Guid.NewGuid(),
        ProjectId: Guid.NewGuid(),
        TriggerListId: Guid.NewGuid(),
        ActionUserId: Guid.NewGuid(),
        CreatedById: Guid.NewGuid()
    );

    private record TestIds(Guid TenantId, Guid ProjectId, Guid TriggerListId, Guid ActionUserId, Guid CreatedById);

    [Fact]
    public void Create_WithValidData_SucceedsAndIsEnabledByDefault()
    {
        var v = Valid();

        var result = AutomationRule.Create(
            v.TenantId, v.ProjectId, "Notify on Done",
            AutomationTriggerType.TaskMovedToList, v.TriggerListId,
            AutomationActionType.NotifyUser, v.ActionUserId, v.CreatedById);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBlankName_Fails()
    {
        var v = Valid();

        var result = AutomationRule.Create(
            v.TenantId, v.ProjectId, "  ",
            AutomationTriggerType.TaskMovedToList, v.TriggerListId,
            AutomationActionType.NotifyUser, v.ActionUserId, v.CreatedById);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutATriggerList_Fails()
    {
        var v = Valid();

        var result = AutomationRule.Create(
            v.TenantId, v.ProjectId, "Notify on Done",
            AutomationTriggerType.TaskMovedToList, Guid.Empty,
            AutomationActionType.NotifyUser, v.ActionUserId, v.CreatedById);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Automation.InvalidTrigger");
    }

    [Fact]
    public void Create_WithoutAnActionUser_Fails()
    {
        var v = Valid();

        var result = AutomationRule.Create(
            v.TenantId, v.ProjectId, "Notify on Done",
            AutomationTriggerType.TaskMovedToList, v.TriggerListId,
            AutomationActionType.NotifyUser, Guid.Empty, v.CreatedById);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Automation.InvalidAction");
    }

    [Fact]
    public void Disable_ThenEnable_TogglesIsEnabled()
    {
        var v = Valid();
        var rule = AutomationRule.Create(
            v.TenantId, v.ProjectId, "Notify on Done",
            AutomationTriggerType.TaskMovedToList, v.TriggerListId,
            AutomationActionType.NotifyUser, v.ActionUserId, v.CreatedById).Value;

        rule.Disable();
        rule.IsEnabled.Should().BeFalse();

        rule.Enable();
        rule.IsEnabled.Should().BeTrue();
    }
}
