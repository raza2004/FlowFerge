namespace FlowForge.Domain.Workflows.Enums;

public enum AutomationActionType
{
    /// <summary>Sends an in-app/real-time notification to ActionUserId.</summary>
    NotifyUser = 0,

    /// <summary>Assigns the task to ActionUserId.</summary>
    AssignUser = 1
}
