#nullable enable

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// Settings that only mean something on Anthropic. Reached via <see cref="ChatOptions.Anthropic" />.
/// </summary>
/// <remarks>
/// Empty today, and that is the honest state rather than an oversight: everything Anthropic
/// currently honours is a concept OpenAI honours too, so it belongs on the shared surface. Stop
/// sequences included - they work here and not on Responses, but the idea is universal and filing
/// it under one provider would bake a temporary API gap into the public API.
///
/// This exists now because the block is where proprietary settings will land - the thinking budget
/// the mapper currently fixes to adaptive, top_k - and adding it later would mean moving properties
/// out of ChatOptions, which is a breaking change. See <see cref="OpenAiChatOptions" /> for the
/// dividing line.
/// </remarks>
public class AnthropicChatOptions
{
    /// <summary>
    /// Copied rather than shared, so resolving a default onto a clone cannot write into the
    /// caller's object. See <see cref="ChatOptions.Clone" />.
    /// </summary>
    internal AnthropicChatOptions Clone()
    {
        return new AnthropicChatOptions();
    }
}
