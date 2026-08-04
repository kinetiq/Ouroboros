using System.ComponentModel;
using Ouroboros.StructuredOutput;

namespace Ouroboros.Test.StructuredOutput;

/// <summary>
/// Pins the shape PropertyDefinitionGenerator produces. That generator is slated for a rewrite
/// when we move off the current provider SDK, so this is the before-picture to rewrite against.
/// </summary>
public class JsonSchemaTests
{
    [Fact]
    public void Schema_Is_Named_After_The_Type_And_Is_Strict()
    {
        var format = Json.GetSchema(typeof(Person));

        Assert.Equal(nameof(Person), format.JsonSchema!.Name);
        Assert.True(format.JsonSchema.Strict);
    }

    [Fact]
    public void Object_Schema_Lists_Its_Properties()
    {
        var schema = Json.GetSchema(typeof(Person)).JsonSchema!.Schema!;

        Assert.Equal("object", schema.Type);
        Assert.NotNull(schema.Properties);
        Assert.Equal(["Name", "Age", "Nickname"], schema.Properties!.Keys);
    }

    [Fact]
    public void Property_Types_Are_Mapped()
    {
        var props = Json.GetSchema(typeof(Person)).JsonSchema!.Schema!.Properties!;

        Assert.Equal("string", props["Name"].Type);
        Assert.Equal("integer", props["Age"].Type);
    }

    [Fact]
    public void Descriptions_Flow_Through()
    {
        var props = Json.GetSchema(typeof(Person)).JsonSchema!.Schema!.Properties!;

        Assert.Equal("What they go by.", props["Nickname"].Description);
    }

    [Fact]
    public void Null_Type_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Json.GetSchema(null!));
    }

    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }

        [Description("What they go by.")]
        public string Nickname { get; set; } = "";
    }
}
