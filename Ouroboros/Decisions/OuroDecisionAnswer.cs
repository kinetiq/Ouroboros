using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Decisions;

/// <summary>
/// Discriminator for <see cref="OuroDecisionAnswer" />.
/// </summary>
/// <remarks>
/// Values are explicit and Unknown is 0, matching OuroBlockKind: a persisted value stays stable
/// across versions, and an answer type this version does not model becomes Unknown.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public enum OuroDecisionAnswerKind
{
    /// <summary>
    /// The API sent an answer type this version of Ouroboros does not model.
    /// </summary>
    Unknown = 0,

    Predicate = 1,

    Choice = 2,

    Score = 3,

    /// <summary>
    /// The model declined to answer the question.
    /// </summary>
    Refusal = 4
}

/// <summary>
/// The answer to one question, under the question's name.
/// </summary>
/// <remarks>
/// Closed, like OuroDecisionQuestion. C# does not check type patterns for exhaustiveness, so <b>a
/// switch over answers must include a discard arm</b>. A refusal can arrive in place of any answer,
/// and new answer types will reach code compiled against an older version.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public abstract record OuroDecisionAnswer
{
    private protected OuroDecisionAnswer(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// The name of the question this answers.
    /// </summary>
    public string Name { get; }

    public abstract OuroDecisionAnswerKind Kind { get; }
}

/// <summary>
/// The answer to an <see cref="OuroPredicate" />.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroPredicateAnswer : OuroDecisionAnswer
{
    internal OuroPredicateAnswer(string name, double probability) : base(name)
    {
        Probability = probability;
    }

    public override OuroDecisionAnswerKind Kind => OuroDecisionAnswerKind.Predicate;

    /// <summary>
    /// How likely the condition is true, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// There is no yes or no here on purpose. Where to draw the line depends on what a false positive
    /// costs you against a false negative. Set the threshold from labelled examples.
    /// </remarks>
    public double Probability { get; }
}

/// <summary>
/// The answer to an <see cref="OuroChoice" />.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroChoiceAnswer : OuroDecisionAnswer
{
    internal OuroChoiceAnswer(string name, string choice, double confidence,
        IReadOnlyList<OuroChoiceProbability> probabilities) : base(name)
    {
        Choice = choice;
        Confidence = confidence;
        Probabilities = probabilities;
    }

    public override OuroDecisionAnswerKind Kind => OuroDecisionAnswerKind.Choice;

    /// <summary>
    /// The value of the option the model picked.
    /// </summary>
    public string Choice { get; }

    /// <summary>
    /// How sure the model is of <see cref="Choice" />, from 0 to 1.
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// The probability of each option. Empty when the API sends none.
    /// </summary>
    public IReadOnlyList<OuroChoiceProbability> Probabilities { get; }
}

/// <summary>
/// The probability the model gave one option of a choice.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroChoiceProbability(string Value, double Probability);

/// <summary>
/// The answer to an <see cref="OuroScore" />.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroScoreAnswer : OuroDecisionAnswer
{
    internal OuroScoreAnswer(string name, double score, double confidence,
        IReadOnlyList<OuroScoreProbability> probabilities) : base(name)
    {
        Score = score;
        Confidence = confidence;
        Probabilities = probabilities;
    }

    public override OuroDecisionAnswerKind Kind => OuroDecisionAnswerKind.Score;

    /// <summary>
    /// The probability-weighted average of the level indexes, starting at 0.
    /// </summary>
    /// <remarks>
    /// Can fall between two levels: 1.1 means mostly level 1, with some weight above it. Round it
    /// only if you need a single level, and see <see cref="Probabilities" /> for the spread.
    /// </remarks>
    public double Score { get; }

    /// <summary>
    /// How sure the model is of <see cref="Score" />, from 0 to 1.
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// The probability of each level. Empty when the API sends none.
    /// </summary>
    public IReadOnlyList<OuroScoreProbability> Probabilities { get; }
}

/// <summary>
/// The probability the model gave one level of a score.
/// </summary>
/// <param name="Value">The level's index, starting at 0.</param>
/// <param name="Label">The level's label.</param>
/// <param name="Probability">The probability of this level, from 0 to 1.</param>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroScoreProbability(int Value, string Label, double Probability);

/// <summary>
/// The model declined to answer this question.
/// </summary>
/// <remarks>
/// Arrives in place of the answer, so it can stand in for any question type. Other questions in the
/// same request can still have answers.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroDecisionRefusal : OuroDecisionAnswer
{
    internal OuroDecisionRefusal(string name, string? reason) : base(name)
    {
        Reason = reason;
    }

    public override OuroDecisionAnswerKind Kind => OuroDecisionAnswerKind.Refusal;

    /// <summary>
    /// Why the model declined, when the API says.
    /// </summary>
    public string? Reason { get; }
}

/// <summary>
/// An answer type this version of Ouroboros does not model.
/// </summary>
/// <remarks>
/// Carries the API's type string as a diagnostic only, as OuroUnknownBlock does. It is not a
/// contract. The raw answer is still in the response's ResponseText.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroUnknownAnswer : OuroDecisionAnswer
{
    internal OuroUnknownAnswer(string name, string providerType) : base(name)
    {
        ProviderType = providerType;
    }

    public override OuroDecisionAnswerKind Kind => OuroDecisionAnswerKind.Unknown;

    public string ProviderType { get; }
}
