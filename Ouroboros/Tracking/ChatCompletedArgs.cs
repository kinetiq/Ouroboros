using System;
using System.Collections.Generic;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.Responses;

namespace Ouroboros.Tracking;

/// <summary>
/// Event arguments for the OnChatCompleted hook.
/// </summary>
/// <remarks>
/// Fired once per provider attempt. Without a fallback chain that is exactly once per ChatAsync,
/// which is how this has always behaved; with one, a call that failed over reports the failed
/// attempt and the successful one separately, in order, so a consumer logging these keeps a record
/// of what the first provider cost.
/// </remarks>
/// <param name="Model">
/// The model this attempt actually ran on - not necessarily the one the caller asked for, since a
/// later attempt in a fallback chain runs on a different model.
/// </param>
/// <param name="DurationMs">
/// How long this attempt took. The response object returned from ChatAsync carries the total across
/// every attempt instead, so the two differ when a call failed over.
/// </param>
/// <param name="Attempt">
/// 1-based position in the fallback chain. Always 1 when no fallback is configured.
/// </param>
/// <param name="NextModel">
/// The model this call failed over to, when it did. Null on the final attempt and on every attempt
/// of a call that never failed over.
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
    Dictionary<string, string>? Variables = null,

    // Appended, deliberately. Both construction sites pass these positionally, so inserting rather
    // than appending would silently shift Variables into Attempt.
    int Attempt = 1,
    OuroModels? NextModel = null
);
