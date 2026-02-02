using System;
using System.Collections.Generic;

namespace Ouroboros.Tracking;

/// <summary>
/// Tracks a logical session across multiple dialogs/prompts.
/// Create one and pass it to all related dialogs to link them together.
/// </summary>
public class SessionTracker
{
    public Guid SessionId { get; } = Guid.NewGuid();
    public List<EntityTag> Tags { get; } = [];

    /// <summary>
    /// Tags this session with a business entity for tracking purposes.
    /// </summary>
    /// <typeparam name="TEnum">The entity type enum (e.g., ChatEntityTypes)</typeparam>
    /// <param name="entityType">The entity type value</param>
    /// <param name="entityId">The ID of the entity</param>
    public SessionTracker WithTag<TEnum>(TEnum entityType, int entityId) where TEnum : Enum
    {
        Tags.Add(new EntityTag(Convert.ToInt32(entityType), entityId));
        return this;
    }
}
