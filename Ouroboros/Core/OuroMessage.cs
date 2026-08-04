namespace Ouroboros.Core;

/// <summary>
/// A single message in a conversation.
/// </summary>
/// <remarks>
/// Provider-neutral by design: this is the type Ouroboros speaks, and each provider's mapper
/// converts it at the boundary. Keep the primary constructor at these two members — additional
/// fields should be added as init-only properties, which is non-breaking.
/// </remarks>
public sealed record OuroMessage(OuroRole Role, string Content)
{
    public static OuroMessage FromSystem(string content) => new(OuroRole.System, content);

    public static OuroMessage FromUser(string content) => new(OuroRole.User, content);

    public static OuroMessage FromAssistant(string content) => new(OuroRole.Assistant, content);
}
