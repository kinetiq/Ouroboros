using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using Ouroboros.StructuredOutput;

namespace Ouroboros.Test.StructuredOutput;

/// <summary>
/// Covers the provider-neutral schema generator.
/// </summary>
/// <remarks>
/// The shapes exercised here are the ones the consuming application actually uses - lists of nested
/// objects, descriptions on properties and on element types, and types carrying no descriptions at
/// all. Anything beyond that must fail loudly rather than be approximated, which is also pinned.
/// </remarks>
public class JsonSchemaTests
{
    [Fact]
    public void A_Null_Type_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => JsonSchemaGenerator.Generate(null!));
    }

    [Fact]
    public void The_Root_Is_A_Strict_Object()
    {
        var schema = JsonSchemaGenerator.Generate(typeof(Person));

        Assert.Equal("object", (string?)schema["type"]);
        Assert.False((bool?)schema["additionalProperties"]);
    }

    /// <summary>
    /// The single most dangerous thing this generator could get wrong. Responses are deserialized
    /// with default System.Text.Json options, which are case-sensitive - so a camelCased schema
    /// yields a successful parse into an object with every property left at its default. Non-null,
    /// no exception, silently empty.
    /// </summary>
    [Fact]
    public void Property_Names_Are_Emitted_Exactly_As_Declared()
    {
        var schema = JsonSchemaGenerator.Generate(typeof(Person));

        Assert.Equal(["Name", "Age", "Nickname"], PropertyNames(schema));
    }

    [Fact]
    public void Scalars_Map_To_Their_Json_Types()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(Scalars))["properties"]!.AsObject();

        Assert.Equal("string", (string?)properties["Text"]!["type"]);
        Assert.Equal("boolean", (string?)properties["Flag"]!["type"]);
        Assert.Equal("integer", (string?)properties["Count"]!["type"]);
        Assert.Equal("number", (string?)properties["Amount"]!["type"]);

        // No JSON scalar exists for these, so a documented string is the honest mapping.
        Assert.Equal("string", (string?)properties["When"]!["type"]);
        Assert.Contains("8601", (string?)properties["When"]!["description"]);
    }

    /// <summary>
    /// Strict mode requires every property listed. Optionality is expressed by the model supplying
    /// an empty value, not by omitting the field - consumers read flags like HasErrors
    /// unconditionally and would NRE on a missing one.
    /// </summary>
    [Fact]
    public void Every_Property_Is_Required()
    {
        var schema = JsonSchemaGenerator.Generate(typeof(Person));

        Assert.Equal(PropertyNames(schema), schema["required"]!.AsArray().Select(x => (string?)x));
    }

    [Fact]
    public void A_Property_Description_Flows_Into_The_Schema()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(Person))["properties"]!.AsObject();

        Assert.Equal("The person's full name.", (string?)properties["Name"]!["description"]);
    }

    [Fact]
    public void A_Type_With_No_Descriptions_Still_Generates()
    {
        var schema = JsonSchemaGenerator.Generate(typeof(Bare));

        Assert.Equal(["Value"], PropertyNames(schema));
    }

    [Fact]
    public void A_List_Of_Objects_Becomes_An_Array_Of_Objects()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(WithList))["properties"]!.AsObject();
        var items = properties["Items"]!;

        Assert.Equal("array", (string?)items["type"]);
        Assert.Equal("object", (string?)items["items"]!["type"]);
        Assert.Equal(["Label", "Rank"], items["items"]!["properties"]!.AsObject().Select(x => x.Key));
    }

    /// <summary>
    /// The one place a type-level [Description] is load-bearing: it describes the element of a
    /// list, which is the only way to tell the model what the items in that array are.
    /// </summary>
    [Fact]
    public void A_Description_On_The_Element_Type_Describes_The_Array_Items()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(WithList))["properties"]!.AsObject();

        Assert.Equal("One scored item.", (string?)properties["Items"]!["items"]!["description"]);
    }

    /// <summary>
    /// A computed property cannot be deserialized into, so asking the model to produce one spends
    /// tokens on a value that is discarded - and invites it to contradict the field it derives
    /// from. The previous generator emitted them.
    /// </summary>
    [Fact]
    public void Get_Only_Properties_Are_Skipped()
    {
        var schema = JsonSchemaGenerator.Generate(typeof(WithComputed));

        Assert.Equal(["Verdict"], PropertyNames(schema));
    }

    [Fact]
    public void An_Enum_Becomes_A_String_With_Its_Names()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(WithEnum))["properties"]!.AsObject();
        var choice = properties["Choice"]!;

        Assert.Equal("string", (string?)choice["type"]);
        Assert.Equal(["Yes", "No"], choice["enum"]!.AsArray().Select(x => (string?)x));
    }

    [Fact]
    public void A_Nullable_Value_Type_Maps_To_Its_Underlying_Type()
    {
        var properties = JsonSchemaGenerator.Generate(typeof(WithNullable))["properties"]!.AsObject();

        Assert.Equal("integer", (string?)properties["Maybe"]!["type"]);
    }

    /// <summary>
    /// The previous generator recursed until the stack died, taking the process with it.
    /// </summary>
    [Fact]
    public void A_Self_Referencing_Type_Throws_Rather_Than_Overflowing()
    {
        var ex = Assert.Throws<NotSupportedException>(() => JsonSchemaGenerator.Generate(typeof(Recursive)));

        Assert.Contains("refers to itself", ex.Message);
    }

    [Fact]
    public void A_Dictionary_Throws_Rather_Than_Emitting_A_Wrong_Shape()
    {
        var ex = Assert.Throws<NotSupportedException>(() => JsonSchemaGenerator.Generate(typeof(WithDictionary)));

        Assert.Contains("dictionaries", ex.Message);
    }

    [Fact]
    public void The_Json_Form_Round_Trips()
    {
        var json = JsonSchemaGenerator.GenerateJson(typeof(Person));

        Assert.Equal("object", (string?)JsonNode.Parse(json)!["type"]);
    }

    // -----------------------------------------------------------------------------------------

    private static IEnumerable<string> PropertyNames(JsonObject schema)
    {
        return schema["properties"]!.AsObject().Select(property => property.Key);
    }

    private class Person
    {
        [Description("The person's full name.")]
        public string Name { get; set; } = "";

        public int Age { get; set; }

        public string Nickname { get; set; } = "";
    }

    private class Scalars
    {
        public string Text { get; set; } = "";
        public bool Flag { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public DateTime When { get; set; }
    }

    private class Bare
    {
        public string Value { get; set; } = "";
    }

    [Description("One scored item.")]
    private class ScoredItem
    {
        public string Label { get; set; } = "";
        public int Rank { get; set; }
    }

    private class WithList
    {
        public List<ScoredItem> Items { get; set; } = [];
    }

    private class WithComputed
    {
        public string Verdict { get; set; } = "";

        public bool IsPass => Verdict == "pass";
    }

    private enum Choice
    {
        Yes,
        No
    }

    private class WithEnum
    {
        public Choice Choice { get; set; }
    }

    private class WithNullable
    {
        public int? Maybe { get; set; }
    }

    private class Recursive
    {
        public string Name { get; set; } = "";
        public Recursive? Child { get; set; }
    }

    private class WithDictionary
    {
        public Dictionary<string, string> Map { get; set; } = [];
    }
}
