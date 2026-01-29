using System;

namespace Ouroboros.Tracking;

/// <summary>
/// Tracks a logical session across multiple dialogs/prompts.
/// Create one and pass it to all related dialogs to link them together.
/// </summary>
public class SessionTracker
{
    public Guid SessionId { get; } = Guid.NewGuid();
}
