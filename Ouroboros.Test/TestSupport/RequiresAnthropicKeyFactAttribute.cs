using System;
using Xunit;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// A fact that runs only when an Anthropic API key is present in the environment.
/// </summary>
/// <remarks>
/// Reports as <b>skipped</b> rather than passing when the key is absent. A live test that quietly
/// passes with nothing configured is worse than no test - it reads as coverage that does not exist.
///
/// The key is read from the environment and never from source or a command line, so it cannot be
/// committed by accident or captured in shell history. Set ANTHROPIC_API_KEY in the session you run
/// the tests from.
/// </remarks>
public sealed class RequiresAnthropicKeyFactAttribute : FactAttribute
{
    internal const string KeyVariable = "ANTHROPIC_API_KEY";

    public RequiresAnthropicKeyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            Skip = $"Live test. Set {KeyVariable} to run it.";
    }

    internal static string? ApiKey => Environment.GetEnvironmentVariable(KeyVariable);
}
