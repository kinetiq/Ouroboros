﻿#nullable enable
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
    public string? GeneratedText { get; set; } 

    public string? GeneratedTextSummary { get; set; } // Not implemented

    public override string ToString()
    {
        if (IsResolved)
        {
            if (GeneratedTextSummary.IsNotNullOrWhiteSpace())
                return Text + GeneratedTextSummary!;

            return GeneratedText.IsNotNullOrWhiteSpace() ?
                Text + GeneratedText! : 
                Text;
        }

        return Text.IsNullOrWhiteSpace() ? 
            Prompt ?? string.Empty : 
            Text;
    }

    public ResolveElement()
    {
        Prompt = null;
        IsResolved = false;
        GeneratedText = string.Empty;
        GeneratedTextSummary = null;
    }
}