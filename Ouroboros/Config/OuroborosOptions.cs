using System;
using System.Collections.Generic;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Config;

/// <summary>
/// Credentials for the providers Ouroboros can route to, and the fallback chain every call uses.
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

    /// <summary>
    /// Models every call falls back to, in order, when the one it asked for cannot serve it.
    /// </summary>
    /// <remarks>
    /// Declared here rather than only through OuroClient.SetDefaultFallback so that a chain naming a
    /// provider with no key fails when the host starts. AddOuroboros validates this during
    /// registration; SetDefaultFallback runs later, when the client is first resolved, which for a
    /// transient registration means the first chat rather than startup.
    ///
    /// That difference matters for a resilience feature. A fallback that was quietly discarded is
    /// discovered during the outage it was configured for.
    ///
    /// An individual call overrides this with ChatOptions.FallbackModels, including an empty list to
    /// opt out. See FAILOVER.md.
    /// </remarks>
    public IList<OuroModels> FallbackModels { get; set; } = [];

    /// <summary>
    /// Throws if this configuration cannot do what it says.
    /// </summary>
    /// <remarks>
    /// Called from AddOuroboros at registration and from OuroClient's constructor, so every way of
    /// building a client fails at the earliest moment the mistake is visible.
    /// </remarks>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(OpenAiApiKey) && string.IsNullOrWhiteSpace(AnthropicApiKey))
        {
            throw new InvalidOperationException(
                "No API key is configured. Set OpenAiApiKey, AnthropicApiKey, or both on "
                + "OuroborosOptions - a client with neither cannot reach any model.");
        }

        foreach (var model in FallbackModels ?? [])
        {
            var provider = model.GetProvider();

            if (HasKeyFor(provider))
                continue;

            throw new InvalidOperationException(
                $"{model} is configured as a fallback, but no {provider} API key is set. Add one to "
                + $"OuroborosOptions.{KeyPropertyFor(provider)}, or take the model out of "
                + "OuroborosOptions.FallbackModels. Failing here rather than at call time, because a "
                + "fallback that is silently unusable is worse than none at all.");
        }
    }

    internal bool HasKeyFor(OuroProvider provider)
    {
        return provider switch
        {
            OuroProvider.OpenAi => !string.IsNullOrWhiteSpace(OpenAiApiKey),
            OuroProvider.Anthropic => !string.IsNullOrWhiteSpace(AnthropicApiKey),
            _ => false
        };
    }

    private static string KeyPropertyFor(OuroProvider provider)
    {
        return provider switch
        {
            OuroProvider.OpenAi => nameof(OpenAiApiKey),
            OuroProvider.Anthropic => nameof(AnthropicApiKey),
            _ => "the matching API key property"
        };
    }
}
