using Ouroboros.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ouroboros.Reflection;
internal static class Helpers
{
    /// <summary>
    /// Returns properties that we can use for reflection purposes.
    /// </summary>
    internal static List<PropertyInfo> GetBindableProperties<T>()
    {
        return GetBindableProperties(typeof(T));
    }

    /// <summary>
    /// Returns properties that we can use for reflection purposes.
    /// </summary>
    internal static List<PropertyInfo> GetBindableProperties(Type type)
    {
        // Extract all public instance properties of T that are able to write to.
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.CanWrite)
            .ToList();
    }

    internal static FieldInfo? GetFieldWithAttribute<TAttribute>(Type type, Func<TAttribute, bool>? predicate = null)
        where TAttribute : Attribute
    {
        predicate ??= _ => true;

        foreach (var field in type.GetFields())
        {
            var attribute = field.GetCustomAttribute<TAttribute>();

            if (attribute != null && predicate(attribute))
                return field;
        }

        return null;
    }


    /// <summary>
    /// If the enum has a NoMatch property, or a value with the  NoMatch attribute, return its value.
    /// </summary>
    internal static object? GetNoMatchValue(Type type)
    {
        if (!type.IsEnum)
            throw new ArgumentException("Property must be an enum.");

        if (Enum.TryParse(type, "NoMatch", ignoreCase: true, out var enumValue))
            return enumValue;

        var field = GetFieldWithAttribute<NoMatchAttribute>(type);

        if (field != null)
            return Enum.Parse(type, field.Name);

        return null;
    }

    /// <summary>
    /// If the enum has a NoMatch property, or a value with the  NoMatch attribute, return its value.
    /// </summary>
    internal static object? GetNoMatchValue(PropertyInfo property)
    {
        var type = property.PropertyType;

         return GetNoMatchValue(type);
    }

    internal static FieldInfo? GetFieldWithAttribute<TEnum, TAttribute>(Func<TAttribute, bool>? predicate = null)
        where TEnum : struct, Enum where TAttribute : Attribute
    {
        var type = typeof(TEnum);

        return GetFieldWithAttribute<TAttribute>(type, predicate);

    }

    internal static FieldInfo? GetFieldWithAttribute<TAttribute>(PropertyInfo property, Func<TAttribute, bool>? predicate = null)
        where TAttribute : Attribute
    {
        var type = property.PropertyType;

        return GetFieldWithAttribute(type, predicate);

    }
}
