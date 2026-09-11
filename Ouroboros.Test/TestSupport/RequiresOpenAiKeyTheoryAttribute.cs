using System.Runtime.CompilerServices;
using Xunit;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// The theory form of <see cref="RequiresOpenAiKeyFactAttribute" />, for a live test that runs the
/// same call over several inputs.
/// </summary>
/// <remarks>
/// Same rules: skipped rather than passed when the key is absent, and the key comes from the
/// environment only. Set OPENAI_API_KEY in the session you run the tests from.
/// </remarks>
public sealed class RequiresOpenAiKeyTheoryAttribute : TheoryAttribute
{
    public RequiresOpenAiKeyTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(RequiresOpenAiKeyFactAttribute.ApiKey))
            Skip = $"Live test. Set {RequiresOpenAiKeyFactAttribute.KeyVariable} to run it.";
    }
}
