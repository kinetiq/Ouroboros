using System;
using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Core;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// Converts Ouroboros primitives into Betalgo's equivalents.
/// </summary>
/// <remarks>
/// Kept separate from ChatMappings on purpose. ChatMappings assembles a provider request and will
/// be duplicated per provider; this file is purely Betalgo-specific and gets deleted outright when
/// we move to the official SDK.
/// </remarks>
internal static class BetalgoMappings
{
    internal static ChatMessage ToBetalgo(this OuroMessage message) => message.Role switch
    {
        OuroRole.System => ChatMessage.FromSystem(message.Content),
        OuroRole.User => ChatMessage.FromUser(message.Content),
        OuroRole.Assistant => ChatMessage.FromAssistant(message.Content),
        _ => throw new ArgumentOutOfRangeException(nameof(message), message.Role, null)
    };

    internal static ReasoningEffort? ToBetalgo(this OuroReasoningEffort? effort)
        => effort.HasValue ? effort.Value.ToBetalgo() : (ReasoningEffort?)null;

    internal static ReasoningEffort ToBetalgo(this OuroReasoningEffort effort) => effort switch
    {
        OuroReasoningEffort.Low => ReasoningEffort.Low,
        OuroReasoningEffort.Medium => ReasoningEffort.Medium,
        OuroReasoningEffort.High => ReasoningEffort.High,
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, null)
    };
}
