using System;
using Xunit;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// A fact that runs only when an OpenAI API key is present in the environment.
/// </summary>
/// <remarks>
/// The sibling of <see cref="RequiresAnthropicKeyFactAttribute" />, and for the same reasons:
/// reports as <b>skipped</b> rather than passing when the key is absent, and reads the key from the
/// environment only, never from source or a command line. Set OPENAI_API_KEY in the session you run
/// the tests from.
/// </remarks>
public sealed class RequiresOpenAiKeyFactAttribute : FactAttribute
{
    internal const string KeyVariable = "OPENAI_API_KEY";

    public RequiresOpenAiKeyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            Skip = $"Live test. Set {KeyVariable} to run it.";
    }

    internal static string? ApiKey => Environment.GetEnvironmentVariable(KeyVariable);
}
