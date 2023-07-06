using System;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Extensions;

public static class GetMaxTokensExtensions
{
    public static int GetMaxTokens(this OuroModels model)
    {
        var field = model
            .GetType()
            .GetField(model.ToString());

        if (field != null && Attribute.GetCustomAttribute(field, typeof(MaxTokensAttribute)) is MaxTokensAttribute attribute)
            return attribute.Tokens;

        throw new ArgumentException("MaxTokens was not set on this model. This should never happen.", nameof(model));
    }
}