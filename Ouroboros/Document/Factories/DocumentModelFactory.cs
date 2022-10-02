﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ouroboros.Document.Elements;

namespace Ouroboros.Document.Factories;

internal class DocumentModelFactory
{
    public const string XmlTagPattern = "<[^>]+>";

    private readonly ElementFactory ElementFactory = new ElementFactory();

    private  List<ElementBase> Elements = new List<ElementBase>();

    private string Text = "";
    private Match? LastMatch = null;
    private bool IsInTag = false;
    private int LastIndex = 0;


    public DocumentModel Create(string text)
    {
        // Reset
        Text = text;
        Elements = new List<ElementBase>();
        LastMatch = null;
        IsInTag = false;
        LastIndex = 0;

        // This effectively gets all tags, for instance <Prompt> would be one and </Prompt> would be another.
        // Also matches </Prompt>. After that, the work becomes getting these into a DOM we can more easily work with.
        var matches = Regex.Matches(Text, XmlTagPattern);

        foreach (Match match in matches)
            ProcessMatch(match);

        if (IsInTag)
            throw new InvalidOperationException($"At position {LastMatch.Index}, {LastMatch.Value} opens and is never closed.");

        // Now that we've checked all our matches, there could still be some text at the end of the document. 
        // That needs to go into the DOM as well.
        CreateTextElementForTextAfterFinalTag(text);

        Validate();

        return new DocumentModel()
        {
            Elements = Elements
        };
    }

    private void Validate()
    {
        var prompt = Elements
            .Where(x => x is PromptElement)
            .ToList();

        if (prompt.Count == 0)
            throw new InvalidOperationException("No Prompt was found. A document must have exactly one Prompt.");

        if (prompt.Count > 1)
            throw new InvalidOperationException($"{prompt.Count} prompt tags were found. A document must have exactly one Prompt.");
    }

    private void ProcessMatch(Match match)
    {
        // Special handling for self-enclosed tags
        if (match.Value.EndsWith("/>"))
        {
            HandleSelfEnclosedTag(match);

            LastIndex = match.Index + match.Length;
            LastMatch = null;
            IsInTag = false;
            return;
        }

        // That leaves Start and End tags. We handle these by finding pairs of tags, and then passing them and any intervening 
        // text into ElementFactory. If tags could contain sub-tags, this would be a bit more difficult, but we only allow
        // a single level of depth. 
        if (IsInTag)
        {
            // If we are in a tag, that means we've found the closing tag. Create an element and add it to the model.
            var subtext = Text.Substring(LastMatch!.Index, (match.Index - LastMatch!.Index) + match.Length);
            var element = ElementFactory.Create(subtext);
            Elements.Add(element);

            LastMatch = null;
            IsInTag = false;
            LastIndex = match.Index + match.Length;
        }
        else
        {
            // If we aren't in a tag, that means we've found a start tag. First, any text up to this tag must be dropped 
            // into a TextElement. Then, we preserve the fact that we've got an open tag and look for the next one.
            CreateTextElementForFreeText(match);

            LastIndex = match.Index + match.Length;
            LastMatch = match;
            IsInTag = true;
        }
    }

    private void HandleSelfEnclosedTag(Match match)
    {
        if (IsInTag)
            throw new InvalidOperationException(
                $"At position {match.Index}, {match.Value} (a self-enclosed tag) was found, " +
                $"however tag {LastMatch.Value} has not been closed, so this is illegal.");

        CreateTextElementForFreeText(match);
    }

    private void CreateTextElementForFreeText(Match match)
    {
        var subtext = Text.Substring(LastIndex, match.Index - LastIndex);

        if (subtext.Length > 0)
        {
            Elements.Add(new TextElement() { Content = subtext });
        }
    }

    private void CreateTextElementForTextAfterFinalTag(string text)
    {
        // If we are at the end of the document, just exit.
        if (text.Length <= LastIndex) 
            return;

        // Get the remaining text and delete any whitespace.
        var subtext = text
            .Substring(LastIndex)
            .Trim();

        // Create the element.
        if (subtext.Length > 0)
            Elements.Add(new TextElement() { Content = subtext });
    }
}