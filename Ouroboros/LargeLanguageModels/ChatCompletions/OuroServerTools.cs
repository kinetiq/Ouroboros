using System;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// Provider-side tools the model may use while answering.
/// </summary>
/// <remarks>
/// These run on the provider's infrastructure, not yours: you declare them, the provider executes
/// them mid-turn, and the results come back as extra blocks on the same response. There is no
/// tool-execution loop to write.
///
/// A flags enum rather than a raw tool-type string on purpose - "code_execution_20260521" is a
/// provider's vocabulary and a version that will move. Callers ask for the capability; the mapper
/// decides which version of it to request.
/// </remarks>
[Flags]
public enum OuroServerTools
{
    None = 0,

    /// <summary>
    /// Lets the model write and run Python in a provider-hosted sandbox, and read back what it
    /// printed or produced. Results arrive as OuroCodeExecutionBlock entries on the response.
    /// </summary>
    /// <remarks>
    /// Not available on every provider - OpenAI's Chat Completions API has no server-side tools at
    /// all. Requesting it on a model that cannot serve it fails rather than being ignored.
    /// </remarks>
    CodeExecution = 1
}
