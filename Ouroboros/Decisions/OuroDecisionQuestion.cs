using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Decisions;

/// <summary>
/// One question in a decision request. The answer comes back under the same <see cref="Name" />.
/// </summary>
/// <remarks>
/// The hierarchy is closed, like OuroContentBlock: the constructor is private protected, so only
/// this assembly can add question types. The mapper can then handle every type there is, and a new
/// one arrives without a breaking change.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public abstract record OuroDecisionQuestion
{
    private protected OuroDecisionQuestion(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Identifies the question. Must be unique within a request.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What to evaluate, in plain language.
    /// </summary>
    /// <remarks>
    /// Required for a predicate, where it is the condition itself. Optional for a choice or a score,
    /// where the options or levels and their descriptions can carry the question on their own.
    /// </remarks>
    public string? Instructions { get; init; }
}

/// <summary>
/// Asks whether a condition is true. The answer is a probability from 0 to 1.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroPredicate : OuroDecisionQuestion
{
    /// <param name="name">Identifies the question in the response.</param>
    /// <param name="instructions">The condition to evaluate.</param>
    public OuroPredicate(string name, string instructions) : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);

        Instructions = instructions;
    }
}

/// <summary>
/// Picks one option from an unordered set. Use <see cref="OuroScore" /> when the options have an order.
/// </summary>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroChoice : OuroDecisionQuestion
{
    /// <param name="name">Identifies the question in the response.</param>
    /// <param name="options">The options to pick from. The answer returns one option's value.</param>
    public OuroChoice(string name, IReadOnlyList<OuroChoiceOption> options) : base(name)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    public IReadOnlyList<OuroChoiceOption> Options { get; }
}

/// <summary>
/// One option of an <see cref="OuroChoice" />.
/// </summary>
/// <param name="Value">What the answer returns when the model picks this option.</param>
/// <param name="Description">What the option means. The model reads this to decide.</param>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroChoiceOption(string Value, string Description);

/// <summary>
/// Rates the input against ordered levels.
/// </summary>
/// <remarks>
/// Put the levels in order, lowest first. The answer's score is the probability-weighted average of
/// the level indexes, starting at 0, so it can fall between two levels.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroScore : OuroDecisionQuestion
{
    /// <param name="name">Identifies the question in the response.</param>
    /// <param name="levels">The levels, lowest first.</param>
    public OuroScore(string name, IReadOnlyList<OuroScoreLevel> levels) : base(name)
    {
        ArgumentNullException.ThrowIfNull(levels);

        Levels = levels;
    }

    public IReadOnlyList<OuroScoreLevel> Levels { get; }
}

/// <summary>
/// One level of an <see cref="OuroScore" />.
/// </summary>
/// <param name="Label">A short name for the level.</param>
/// <param name="Description">What the level means. The model reads this to decide.</param>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroScoreLevel(string Label, string Description);
