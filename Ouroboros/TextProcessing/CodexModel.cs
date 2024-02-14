using Ouroboros.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ouroboros.TextProcessing;
public abstract class CodexModel
{
    /// <summary>
    /// Tracks properties that were not bound, which can be useful for debugging.
    /// </summary>
    internal List<string> UnboundProperties = new();

    public bool IsComplete()
    {
        if (UnboundProperties.Any())
            return false;

        return AllEnumsAreMatched();
    }

    /// <summary>
    /// Returns false if any enum properties are set to NoMatch. NoMatch could either happen if the properties were not
    /// targeted for binding, or if the text could not be matched to the enum.
    /// </summary>
    private bool AllEnumsAreMatched()
    {
        var properties = GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x is { CanWrite: true, PropertyType.IsEnum: true });

        foreach (var property in properties)
        {
            var value = property.GetValue(this);
            var noMatchValue = Helpers.GetNoMatchValue(property);

            if (value == null || noMatchValue == null)
                continue;

            if (value.Equals(noMatchValue))
                return false;
        }

        return true;
    }
}
