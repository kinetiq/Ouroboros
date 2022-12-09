using System;
using System.Diagnostics;

namespace Ouroboros.Documents.Elements;

[Serializable]
[DebuggerDisplay("Prompt: { Text }")]
public class PromptElement : ElementBase
{
    public override string Type()
    {
        return "Prompt";
    }
}