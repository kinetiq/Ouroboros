using System;

namespace Ouroboros.Tracking;

/// <summary>
/// Tracks prompts within a single dialog chain.
/// </summary>
public class ChainTracker
{
    public Guid ChainId { get; } = Guid.NewGuid();
}
