namespace Ouroboros.Tracking;

/// <summary>
/// Represents a link between a chat thread/session and a business entity.
/// </summary>
/// <param name="EntityType">The type of entity (as int to avoid Ouroboros depending on Keystone enums)</param>
/// <param name="EntityId">The ID of the entity in its respective table</param>
public record EntityTag(int EntityType, int EntityId);
