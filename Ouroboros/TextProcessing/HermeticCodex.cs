using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Ouroboros.Extensions;
using Ouroboros.Reflection;

namespace Ouroboros.TextProcessing;

public class HermeticCodex<T> where T : CodexModel
{
    private Dictionary<string, string> Bindings = new(StringComparer.InvariantCultureIgnoreCase);
    private StringBuilder Buffer = new();
    private string CurrentKey = "Intro"; // default key

    public T Bind(string text)
    {
        // Reset
        Bindings = new Dictionary<string, string>();
        Buffer = new StringBuilder();
        CurrentKey = "Intro";

        var lines = text
            .SplitTextOnNewLines(StringSplitOptions.TrimEntries)
            .ToList();

        ParseBindings(lines);

        var instance = Activator.CreateInstance<T>();

        // Create a new instance of T, and bind it.
        return BindInstance(instance);
    }

    private T BindInstance(T instance)
    {
        var properties = Helpers.GetBindableProperties<T>();

        // Loop through all public / writable properties on our instance of T. Bind them if we can.
        foreach (var property in properties)
        {
            // TODO: aliases for property names, or maybe remove spaces to make things map easier.

            if (Bindings.ContainsKey(property.Name))
            {
                BindValue(instance, property, Bindings[property.Name]);
                continue;
            }

            instance.UnboundProperties.Add(property.Name);
        }

        return instance;
    }

    private void BindValue(T instance, PropertyInfo property, string value)
    {
        // If property is a string, set it.
        if (property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return;
        }

        if (property.PropertyType == typeof(bool))
        {
            var booleanValue = ProteusConvert.ToBool(value);

            property.SetValue(instance, booleanValue);
            return;
        }

        if (property.PropertyType.IsEnum)
        {
            var enumValue = ProteusConvert.ToEnum(property.PropertyType, value);
            property.SetValue(instance, enumValue);

            return;
        }

        throw new NotImplementedException(
            $"While binding {typeof(T).Name}, property {property.Name} had an unhandled type ({property.PropertyType.Name}). " +
            $"The string value we tried to bind was: {value}");
    }

    #region Parsing

    private void ParseBindings(List<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.IsMarkdownHeading())
            {
                OnHeadingFound(line);
                continue;
            }

            Buffer.AppendLine(line);
        }

        if (Buffer.Length > 0)
            Bindings.Add(CurrentKey, Buffer.ToString().TrimEnd());
    }

    private void OnHeadingFound(string heading)
    {
        Bindings.Add(CurrentKey, Buffer.ToString().TrimEnd());
        Buffer.Clear();

        CurrentKey = heading.ExtractHeadingText();
    }

    #endregion
}

public class BindResult<T> where T : class
{
    /// <summary>
    ///     True if all properties were successfully bound.
    /// </summary>
    public bool IsComplete { get; set; } = false;

    public T Model { get; set; }

    public BindResult(T model)
    {
        Model = model;
    }
}