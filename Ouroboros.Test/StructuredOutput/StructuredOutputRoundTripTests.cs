using System.Linq;
using System.Text.Json.Nodes;
using Ouroboros.StructuredOutput;

namespace Ouroboros.Test.StructuredOutput;

/// <summary>
/// The schema and the parser have to agree, not merely each be defensible alone.
/// </summary>
/// <remarks>
/// JsonSchemaTests pins what the generator emits. Nothing checked what came back, and a bug
/// shipped in the gap. Enums were emitted as strings, which is what strict mode wants, but the
/// parser then accepted only the number. Every ResponseType with an enum on it produced a
/// successful response whose ResponseObject was silently null.
///
/// These tests go the whole way round: generate the schema, feed back exactly the JSON it asks a
/// model for, assert a populated object. A schema assertion could not have caught this, because
/// the schema was never the thing that was wrong.
/// </remarks>
public class StructuredOutputRoundTripTests
{
    [Fact]
    public void An_Enum_Property_Round_Trips()
    {
        // Exactly the shape the schema requests: the member's name, not its number.
        var parsed = ResponseParser.Parse(typeof(Verdict), """{"Choice":"No","Note":"because"}""");

        var verdict = Assert.IsType<Verdict>(parsed);

        Assert.Equal(Choice.No, verdict.Choice);
        Assert.Equal("because", verdict.Note);
    }

    /// <summary>
    /// The schema really does ask for the name, so the test above is testing the real contract.
    /// </summary>
    [Fact]
    public void The_Schema_Asks_For_The_Enum_By_Name()
    {
        var choice = JsonSchemaGenerator.Generate(typeof(Verdict))["properties"]!["Choice"]!.AsObject();

        Assert.Equal("string", (string?)choice["type"]);
        Assert.Equal(["Yes", "No"], choice["enum"]!.AsArray().Select(value => (string?)value));
    }

    [Fact]
    public void A_Nullable_Enum_Property_Round_Trips()
    {
        var parsed = ResponseParser.Parse(typeof(Maybe), """{"Choice":"Yes"}""");

        Assert.Equal(Choice.Yes, Assert.IsType<Maybe>(parsed).Choice);
    }

    /// <summary>
    /// A name the enum does not have still parses to null rather than throwing.
    /// </summary>
    /// <remarks>
    /// The converter must not turn a bad value into an exception escaping ChatAsync. Null is the
    /// documented signal for "it did not parse", and every consumer branches on it.
    /// </remarks>
    [Fact]
    public void An_Unknown_Enum_Name_Parses_To_Null()
    {
        Assert.Null(ResponseParser.Parse(typeof(Verdict), """{"Choice":"Perhaps","Note":"x"}"""));
    }

    /// <summary>
    /// Casing is still matched exactly, which is what the generator's PascalCase rule relies on.
    /// </summary>
    /// <remarks>
    /// Guarding the fix rather than the bug: adding the enum converter must not drag in
    /// case-insensitive property matching, or a schema emitting the wrong casing would start
    /// appearing to work while every field came back empty.
    /// </remarks>
    [Fact]
    public void Property_Names_Are_Still_Matched_Case_Sensitively()
    {
        var parsed = ResponseParser.Parse(typeof(Verdict), """{"choice":"No","note":"because"}""");

        // Parsed, but nothing landed: the camelCase names matched no property.
        var verdict = Assert.IsType<Verdict>(parsed);

        Assert.Equal(default, verdict.Choice);
        Assert.Null(verdict.Note);
    }

    private enum Choice
    {
        Yes,
        No
    }

    private class Verdict
    {
        public Choice Choice { get; set; }

        public string? Note { get; set; }
    }

    private class Maybe
    {
        public Choice? Choice { get; set; }
    }
}
