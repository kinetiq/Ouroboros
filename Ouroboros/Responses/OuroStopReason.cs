namespace Ouroboros.Responses;

/// <summary>
/// Why the model stopped generating.
/// </summary>
/// <remarks>
/// Explicit values with Unknown at 0, so persisted numbers stay stable and an unmapped provider
/// value degrades rather than throwing.
/// </remarks>
public enum OuroStopReason
{
    /// <summary>
    /// The provider reported a reason this version does not model, or reported none at all.
    /// Treated as complete - only a positive signal marks a response as cut short.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The model finished on its own.
    /// </summary>
    EndTurn = 1,

    /// <summary>
    /// The output hit the token ceiling and is truncated mid-thought.
    /// </summary>
    /// <remarks>
    /// Worth checking. Truncated structured output simply fails to parse, which surfaces as a null
    /// ResponseObject on an otherwise successful response - indistinguishable, without this, from a
    /// model that legitimately returned nothing.
    /// </remarks>
    MaxTokens = 2,

    /// <summary>
    /// Generation stopped at one of the caller's stop sequences.
    /// </summary>
    StopSequence = 3,

    /// <summary>
    /// A provider-side tool loop hit its iteration cap. The response is successful but incomplete,
    /// and continuing it requires sending the turn back.
    /// </summary>
    /// <remarks>
    /// Ouroboros continues paused turns for you, up to Constants.MaxPausedTurnContinuations, so
    /// this only reaches a caller when a turn paused more times than that. The response is still
    /// successful and carries everything the turn produced across every round - it is unfinished,
    /// not failed, which is what IsComplete reports.
    /// </remarks>
    Paused = 4
}
