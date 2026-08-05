namespace Ouroboros.Config;

/// <summary>
/// Credentials for the providers Ouroboros can route to.
/// </summary>
/// <remarks>
/// An options object rather than more positional parameters: providers are added over time, and
/// AddOuroboros(services, openAiKey, anthropicKey, ...) becomes unreadable by the third one and
/// breaking every time the list grows.
///
/// Only the providers you actually use need a key. Requesting a model whose provider has no key
/// configured fails with a message naming the provider, rather than an auth error from a vendor you
/// did not think you were calling.
/// </remarks>
public sealed class OuroborosOptions
{
    /// <summary>
    /// Required to use any GPT model.
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// Required to use any Claude model.
    /// </summary>
    public string? AnthropicApiKey { get; set; }
}
