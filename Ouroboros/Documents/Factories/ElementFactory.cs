using System;
using System.Xml.Linq;
using Ouroboros.Documents.Elements;
using Z.Core.Extensions;

namespace Ouroboros.Documents.Factories;

/// <summary>
/// Generates elements of various types from raw text.
/// </summary>
internal class ElementFactory
{
    public ElementBase Create(string text)
    {
        var doc = XDocument.Parse(text);
        var element = doc.Root;

        return element!.Name.LocalName.ToLower() switch
        {
            "prompt" => CreatePromptElement(element),
            "text" => CreateTextElement(element),
            "resolve" => CreateResolveElement(element),
            _ => throw new InvalidOperationException("Unexpected name: " + element!.Name.LocalName)
        };
    }

    private ElementBase CreateResolveElement(XElement element)
    {
        var promptAttr = element.Attribute("Prompt");

        return new ResolveElement()
        {
            Prompt = promptAttr?.Value,
            Text = PrepareContent(element)
        };
    }

    private ElementBase CreateTextElement(XElement element)
    {
        return new TextElement()
        {
            Text = PrepareContent(element)
        };
    }

    private ElementBase CreatePromptElement(XElement element)
    {
        return new PromptElement()
        {
            Text = PrepareContent(element)
        };
    }

    /// <summary>
    /// Because of the structure of xml tags, there can be an extra newline that should not
    /// be included. 
    /// </summary>
    private string PrepareContent(XElement element)
    {
        var content = element.Value;

        return content; // TODO: Currently figuring out what to do here. Sometimes the whitespace is wanted, sometimes not.

        // Cut only the final whitespace, since it is an artifact caused by the tag system.
        if (content.EndsWith("\n"))
            content = content.Left(content.Length - 1);

        return content;
    }
}