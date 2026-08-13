using System;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Extensions;

public static class GetMaxTokensExtensions
{
    /// <summary>
    /// Gets the context window size (max input tokens) for this model.
    /// </summary>
    public static int GetContextWindow(this OuroModels model)
    {
        var attribute = GetMaxTokensAttribute(model);
        return attribute.ContextWindow;
    }

    /// <summary>
    /// Gets the maximum output tokens this model can generate.
    /// </summary>
    public static int GetMaxOutputTokens(this OuroModels model)
    {
        var attribute = GetMaxTokensAttribute(model);
        return attribute.MaxOutput;
    }

    private static MaxTokensAttribute GetMaxTokensAttribute(OuroModels model)
    {
        var field = model
            .GetType()
            .GetField(model.ToString());

        if (field != null && Attribute.GetCustomAttribute(field, typeof(MaxTokensAttribute)) is MaxTokensAttribute attribute)
            return attribute;

        throw new ArgumentException("MaxTokens was not set on this model. This should never happen.", nameof(model));
    }

    /// <summary>
    /// Returns true if this model supports reasoning (thinking tokens).
    /// Reasoning models may use additional tokens for internal reasoning and support the ReasoningEffort parameter.
    /// </summary>
    public static bool IsReasoningModel(this OuroModels model)
    {
        var field = model
            .GetType()
            .GetField(model.ToString());

        return field != null && Attribute.GetCustomAttribute(field, typeof(ReasoningAttribute)) is ReasoningAttribute;
    }

    /// <summary>
    /// Which vendor's API serves this model, and therefore which client the request is routed to.
    /// </summary>
    public static OuroProvider GetProvider(this OuroModels model)
    {
        var field = model
            .GetType()
            .GetField(model.ToString());

        if (field != null && Attribute.GetCustomAttribute(field, typeof(ProviderAttribute)) is ProviderAttribute attribute)
            return attribute.Provider;

        throw new ArgumentException(
            $"No provider is set on {model}. Every model must declare one - without it there is " +
            "nothing to route the request to.", nameof(model));
    }
}