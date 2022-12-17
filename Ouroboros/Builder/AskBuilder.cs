using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ouroboros.Builder;

public class AskBuilder : IAsker
{
    public Document Document { get; set; }
    public string ResponseText { get; set; }


    public async Task<AskBuilder> Ask(string text, string newElementName = "")
    {
        Document.AddText(text);
        var element = await Document.ResolveAndSubmit(newElementName);

        return new AskBuilder(Document, element.Text);
    }

    public AskBuilder RemoveLast()
    {
        var element = Document.GetLastGeneratedAsElement();

        if (Document.LastResolvedElement == element)
            Document.LastResolvedElement = null;

        Document.DocElements.Remove(element);

        return this;
    }

    public async Task<AskBuilder> RemoveLastAndAsk(string text, string newElementName = "")
    {
        RemoveLast();
        return await Ask(text, newElementName);
    } 
    
    /// <summary>
    /// Assumes the last prompt returned was a text-based list of some kind, with items separated by newlines.
    /// Cleans up any extra whitespace and returns list().
    /// </summary>
    public List<string> AsList()
    {
        var last = Document.GetLastGeneratedAsElement();

        var options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        // turn the text from the last element into a list by splitting on newlines
        var lineList =
            last.SplitTextOnNewLines(options)
                .ToList();

        return lineList.ToList();
    }

    public List<string> AsLikert5()
    {
        return new List<string>();
    }


    public AskBuilder(Document document, string responseText)
    {
        Document = document;
        ResponseText = responseText;
    }

}