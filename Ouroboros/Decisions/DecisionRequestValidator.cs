using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Ouroboros.Decisions;

/// <summary>
/// Finds the mistakes in a decision request that only the whole request shows, before a call is
/// spent on them.
/// </summary>
/// <remarks>
/// The types refuse what they can at construction: a blank name, a non-image media type. What is
/// left needs the whole request (unique names, some evidence) or slips past a constructor (a with
/// expression that clears a predicate's instructions, a required list set to null).
///
/// Reports every problem at once rather than the first. A request with three mistakes should not
/// take three runs to fix.
///
/// Only rules that hold whatever the API does are checked here. Limits the guide does not state,
/// such as how many questions fit in one request, are left to the API to report.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
internal static class DecisionRequestValidator
{
    /// <returns>Null when the request can be sent; otherwise every problem, one per line.</returns>
    public static string? Validate(OuroDecisionRequest request, DecisionOptions options)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Model))
            problems.Add("DecisionOptions.Model is empty. Leave it at its default, or name a Decisions model.");

        if (options.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            problems.Add($"DecisionOptions.Timeout is {timeout}. Use a positive value, or null for the default.");

        CheckEvidence(request, problems);
        CheckQuestions(request, problems);

        return problems.Count == 0
            ? null
            : "The decision request was not sent:" + Environment.NewLine
              + string.Join(Environment.NewLine, problems.Select(problem => "- " + problem));
    }

    private static void CheckEvidence(OuroDecisionRequest request, List<string> problems)
    {
        if (request.Images is null)
        {
            problems.Add("Images is null. Use an empty list when there are no images.");
            return;
        }

        if (request.Images.Any(image => image is null))
            problems.Add("Images contains a null entry.");

        if (string.IsNullOrWhiteSpace(request.Text) && request.Images.Count == 0)
            problems.Add("There is nothing to evaluate. Set Text, add an image, or both.");
    }

    private static void CheckQuestions(OuroDecisionRequest request, List<string> problems)
    {
        if (request.Questions is null || request.Questions.Count == 0)
        {
            problems.Add("There are no questions. Add at least one.");
            return;
        }

        if (request.Questions.Any(question => question is null))
        {
            problems.Add("Questions contains a null entry.");
            return;
        }

        // Ordinal: the API matches answers to questions by name, and nothing says it folds case.
        foreach (var name in Duplicates(request.Questions.Select(question => question.Name)))
            problems.Add($"More than one question is named '{name}'. Each answer comes back under its question's name, so names must be unique.");

        foreach (var question in request.Questions)
        {
            switch (question)
            {
                case OuroPredicate predicate when string.IsNullOrWhiteSpace(predicate.Instructions):
                    problems.Add($"Predicate '{predicate.Name}' has no instructions. They are the condition to evaluate.");
                    break;

                case OuroChoice choice:
                    CheckChoice(choice, problems);
                    break;

                case OuroScore score:
                    CheckScore(score, problems);
                    break;
            }
        }
    }

    private static void CheckChoice(OuroChoice choice, List<string> problems)
    {
        if (choice.Options.Count < 2)
        {
            problems.Add($"Choice '{choice.Name}' has {choice.Options.Count} option(s). A choice needs at least two.");
            return;
        }

        if (choice.Options.Any(option => option is null))
        {
            problems.Add($"Choice '{choice.Name}' has a null option.");
            return;
        }

        if (choice.Options.Any(option => string.IsNullOrWhiteSpace(option.Value)))
            problems.Add($"Choice '{choice.Name}' has an option with no value. The value is what the answer returns.");

        if (choice.Options.Any(option => string.IsNullOrWhiteSpace(option.Description)))
            problems.Add($"Choice '{choice.Name}' has an option with no description. The model reads it to decide.");

        foreach (var value in Duplicates(choice.Options.Select(option => option.Value)))
            problems.Add($"Choice '{choice.Name}' has more than one option with the value '{value}'. The answer could not say which one was picked.");
    }

    private static void CheckScore(OuroScore score, List<string> problems)
    {
        if (score.Levels.Count < 2)
        {
            problems.Add($"Score '{score.Name}' has {score.Levels.Count} level(s). A score needs at least two.");
            return;
        }

        if (score.Levels.Any(level => level is null))
        {
            problems.Add($"Score '{score.Name}' has a null level.");
            return;
        }

        if (score.Levels.Any(level => string.IsNullOrWhiteSpace(level.Label)))
            problems.Add($"Score '{score.Name}' has a level with no label.");

        if (score.Levels.Any(level => string.IsNullOrWhiteSpace(level.Description)))
            problems.Add($"Score '{score.Name}' has a level with no description. The model reads it to decide.");

        foreach (var label in Duplicates(score.Levels.Select(level => level.Label)))
            problems.Add($"Score '{score.Name}' has more than one level labelled '{label}'.");
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
    }
}
