#nullable enable
using System;
using System.Diagnostics;
using Z.Core.Extensions;

namespace Ouroboros.Document.Elements;

[Serializable]
[DebuggerDisplay("Resolve: { ToString }")]
internal class ResolveElement : ElementBase
{
    // Attributes set via element
    public string? Prompt { get; set; }

    // Attributes set via resolver
    public bool IsResolved { get; set; }
    public string? GeneratedOutput { get; set; } 

    public string? GeneratedOutputSummary { get; set; } // Not implemented

    public override string ToString()
    {
        if (IsResolved)
        {
            if (GeneratedOutputSummary.IsNotNullOrWhiteSpace())
                return Content + GeneratedOutputSummary!;

            return GeneratedOutput.IsNotNullOrWhiteSpace() ?
                Content + GeneratedOutput! : 
                Content;
        }

        return Content.IsNullOrWhiteSpace() ? 
            Prompt ?? string.Empty : 
            Content;
    }

    public ResolveElement()
    {
        Prompt = null;
        IsResolved = false;
        GeneratedOutput = string.Empty;
        GeneratedOutputSummary = null;
    }
}