#nullable enable
namespace Ouroboros.Documents;

internal class ResolveOptions
{
    public bool SubmitResultForCompletion { get; set; } = false;
    public string NewElementName { get; set; } = string.Empty; 
    public bool HaltAfterFirstComplete = false;
}
