using System;
using System.Collections.Generic;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.Responses;

namespace Ouroboros.Tracking;

/// <summary>
/// Event arguments for the OnChatCompleted hook.
/// </summary>
/// <param name="Model">
/// The model the request actually ran on. Non-nullable even though ChatOptions.Model is optional:
/// ChatAsync resolves it against the client default before firing this hook, so by this point a
/// model is always known.
/// </param>
public record ChatCompletedArgs(
    string? PromptName,
    Guid? SessionId,
    Guid? ThreadId,
    IReadOnlyList<OuroMessage> Messages,
    OuroResponseBase Response,
    OuroModels Model,
    OuroReasoningEffort? ReasoningEffort,
    int DurationMs,
    List<EntityTag> ThreadTags,
    List<EntityTag> SessionTags,
    Dictionary<string, string>? Variables = null
);
