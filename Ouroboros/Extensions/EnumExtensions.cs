using System;
using System.Collections.Generic;

namespace Ouroboros.Extensions;
internal static class EnumExtensions
{
    /// <summary>
    /// True if the enum value is NoMatch.
    /// </summary>
    internal static bool IsNoMatch<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        var noMatchMember = typeof(TEnum).GetField("NoMatch");

        if (noMatchMember == null)
            throw new InvalidOperationException($"The enum {typeof(TEnum).Name} does not have a 'NoMatch' member.");

        var noMatchValue = (TEnum)noMatchMember.GetValue(null)!;
        
        return EqualityComparer<TEnum>.Default.Equals(enumValue, noMatchValue);
    }
}
