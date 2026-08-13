namespace QuickConvert.App;

public enum WindowCloseAction
{
    Close,
    HideToTray,
    KeepVisible
}

public static class BackgroundBehaviorPolicy
{
    public static bool ShouldShowForEnvelope(
        string operation,
        bool runInBackgroundDuringJobs) =>
        !runInBackgroundDuringJobs ||
        !string.Equals(operation, "download", StringComparison.OrdinalIgnoreCase);

    public static WindowCloseAction GetCloseAction(
        bool hasActiveJobs,
        bool runInBackgroundDuringJobs)
    {
        if (!hasActiveJobs)
            return WindowCloseAction.Close;
        return runInBackgroundDuringJobs
            ? WindowCloseAction.HideToTray
            : WindowCloseAction.KeepVisible;
    }

    public static bool ShouldShowTray(
        bool hasActiveJobs,
        bool runInBackgroundDuringJobs) =>
        hasActiveJobs && runInBackgroundDuringJobs;
}
