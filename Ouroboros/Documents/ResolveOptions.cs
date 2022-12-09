#nullable enable
namespace Ouroboros.Documents;

public class ResolveOptions
{
    public bool SubmitResultForCompletion { get; set; } = false;
    public string NewElementName { get; set; } = string.Empty; 

    public bool HaltAfterFirstComplete = false;
}
