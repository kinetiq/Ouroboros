using System;

namespace Ouroboros.Tracking;

/// <summary>
/// Tracks prompts within a single dialog thread.
/// </summary>
public class ThreadTracker
{
    public Guid ThreadId { get; } = Guid.NewGuid();
}
