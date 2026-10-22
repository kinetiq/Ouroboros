using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Ouroboros.Responses;

namespace Ouroboros.Decisions;

/// <summary>
/// A successful decision call: one answer per question.
/// </summary>
/// <remarks>
/// Derives from OuroResponseBase, not OuroResponseSuccess. Content, StopReason and ResponseObject
/// describe generated text, and a decision generates none. So match on this type, not on
/// OuroResponseSuccess. Failures are the same OuroResponseProviderError and OuroResponseInternalError
/// that ChatAsync returns.
///
/// <see cref="OuroResponseBase.ResponseText" /> holds the raw JSON body, so the full record survives
/// even when an answer arrives as <see cref="OuroUnknownAnswer" />.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed class OuroDecisionResponse : OuroResponseBase
{
    /// <summary>
    /// Internal because consumers receive responses, they do not build them.
    /// </summary>
    internal OuroDecisionResponse(IReadOnlyList<OuroDecisionAnswer> answers, string responseText)
    {
        ArgumentNullException.ThrowIfNull(answers);

        Success = true;
        Answers = answers;
        ResponseText = responseText ?? "";
    }

    /// <summary>
    /// The model that answered, as the API reports it.
    /// </summary>
    public string Model { get; init; } = "";

    /// <summary>
    /// Every answer, in the order the API returned them.
    /// </summary>
    public IReadOnlyList<OuroDecisionAnswer> Answers { get; }

    /// <summary>
    /// The answer to the named question, if it has the type asked for.
    /// </summary>
    /// <returns>
    /// Null when no answer has that name, or when the answer is a different type. A refusal is the
    /// usual cause of the second: ask for <see cref="OuroDecisionRefusal" /> to see it.
    /// </returns>
    public T? Get<T>(string name) where T : OuroDecisionAnswer
    {
        return Answers.FirstOrDefault(answer => answer.Name == name) as T;
    }

    /// <summary>
    /// True when the model declined at least one question.
    /// </summary>
    public bool HasRefusals => Answers.Any(answer => answer is OuroDecisionRefusal);
}
