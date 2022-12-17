using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Ouroboros.Documents.Elements;
using Ouroboros.Documents.Extensions;
using Z.Collections.Extensions;
using Z.Core.Extensions;

namespace Ouroboros.Documents.Factories;

internal class PromptFinder
{
    private readonly List<ElementBase> DocElements;

    /// <summary>
    /// If there is no Prompt, try to create one. This only works if the first element is a
    /// TextElement and it contains content.
    /// </summary>
    public void FindPrompt()
    {
        // GUARD: if there's already a prompt, due to using the prompt tag, we can exit.  
        if (DocElements.Any(x => x is PromptElement))
            return;

        // GUARD: only proceed if there are elements and the first one is a TextElement.
        if (DocElements.Count == 0 || DocElements.First() is not TextElement)
            return;

        var textElement = (TextElement) DocElements.First();

        // Split this on newlines. We end up with an array of lines.
        var lines = textElement.SplitTextOnNewLines();

        // Our prompt is going to be the first line that has non-whitespace content.
        var firstRealLine = GetFirstNoneWhitespaceLine(lines);

        // If there are no lines that have content, we are out of luck and we must exit.
        if (firstRealLine == null)
            return;

        CreateAndInsertPrompt(firstRealLine);
        UpdateOrRemoveTextElement(lines, firstRealLine, textElement);
    }

    #region Helpers
    private static string? GetFirstNoneWhitespaceLine(string[] lines)
    {
        return lines.FirstOrDefault(x => x.IsNotNullOrWhiteSpace());
    }
    
    /// <summary>
    /// Creates our prompt and adds it to the beginning of DocElements.
    /// </summary>
    private void CreateAndInsertPrompt(string firstRealLine)
    {
        var prompt = new PromptElement()
        {
            Text = firstRealLine
        };

        DocElements.Insert(0, prompt);
    }

    /// <summary>
    /// We took content from textElement and put it into our new prompt. Therefore,
    /// we need to remove that content from the TextElement or it will be duplicated.
    /// This could also result in TextElement being empty, in which case we remove it.
    /// </summary>
    private void UpdateOrRemoveTextElement(string[] lines, string firstRealLine, TextElement textElement)
    {
        var index = lines.IndexOf(firstRealLine);

        textElement.Text = lines
            .Where(x => lines.IndexOf(x) > index)
            .ToList()
            .StringJoin("\n");

        if (textElement.Text.IsNullOrWhiteSpace())
            DocElements.Remove(textElement);
    }

    #endregion

    public PromptFinder(List<ElementBase> docElements)
    {
        DocElements = docElements;
    }
}