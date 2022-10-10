using System;
using System.Diagnostics;
using Z.Core.Extensions;

namespace Ouroboros.Document.Elements;

[Serializable]
[DebuggerDisplay("Resolve: { ToString }")]
internal class ResolveElement : ElementBase
{
    // Attributes set via document
    public string Prompt { get; set; }

    // Attributes set via resolver
    public bool IsResolved { get; set; }
    public string FullText { get; set; } 
    public string Summary { get; set; } // Not implemented

    public override string ToString()
    {
        if (Content.IsNullOrWhiteSpace())
            return Prompt;

        if (Summary.IsNotNullOrWhiteSpace())
            return Summary;

        return FullText;
    }

    public ResolveElement()
    {
        Prompt = string.Empty;
        IsResolved = false;
        FullText = string.Empty;
        Summary = string.Empty;
    }
}