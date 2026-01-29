namespace Ouroboros.Tracking;

/// <summary>
/// Static factory for creating tracking objects.
/// </summary>
public static class Tracker
{
    /// <summary>
    /// Creates a new SessionTracker for grouping related dialogs.
    /// </summary>
    public static SessionTracker CreateSession() => new();

    /// <summary>
    /// Creates a new ThreadTracker for tracking prompts within a dialog.
    /// </summary>
    public static ThreadTracker CreateThread() => new();
}
