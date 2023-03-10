#nullable enable
namespace Ouroboros.Documents;

public class ResolveOptions
{
    public bool SubmitResultForCompletion { get; set; } = false;
    public string? NewElementName { get; set; } 

    public bool HaltAfterFirstComplete = false;
}
