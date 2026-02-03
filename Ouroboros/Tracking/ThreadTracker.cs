using System;
using System.Collections.Generic;

namespace Ouroboros.Tracking;

/// <summary>
/// Tracks prompts within a single dialog thread.
/// </summary>
public class ThreadTracker
{
    public Guid ThreadId { get; } = Guid.NewGuid();
    public List<EntityTag> Tags { get; } = [];

    /// <summary>
    /// Tags this thread with a business entity for tracking purposes.
    /// </summary>
    /// <typeparam name="TEnum">The entity type enum (e.g., ChatEntityTypes)</typeparam>
    /// <param name="entityType">The entity type value</param>
    /// <param name="entityId">The ID of the entity</param>
    public ThreadTracker WithTag<TEnum>(TEnum entityType, int entityId) where TEnum : Enum
    {
        Tags.Add(new EntityTag(Convert.ToInt32(entityType), entityId));
        return this;
    }
}
