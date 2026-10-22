using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Decisions;

/// <summary>
/// The evidence to evaluate, and the questions to answer about it.
/// </summary>
/// <remarks>
/// Every question is answered against the same evidence, in one call. A question that depends on
/// another question's answer needs a request of its own.
///
/// Supply text, images, or both. A request with neither is refused before any call is made.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroDecisionRequest
{
    /// <summary>
    /// The text to evaluate.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Images to evaluate, alongside the text.
    /// </summary>
    public IReadOnlyList<OuroDecisionImage> Images { get; init; } = [];

    /// <summary>
    /// What to decide. Each question needs a unique name.
    /// </summary>
    public required IReadOnlyList<OuroDecisionQuestion> Questions { get; init; }
}
