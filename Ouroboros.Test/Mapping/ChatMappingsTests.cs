using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;

namespace Ouroboros.Test.Mapping;

public class ChatMappingsTests
{
    private static List<OuroMessage> OneMessage() => [OuroMessage.FromUser("hi")];

    [Fact]
    public void Model_Name_Is_Mapped_To_The_Api_String()
    {
        var request = ChatMappings.MapOptions(OneMessage(), new ChatOptions { Model = OuroModels.Gpt_5_4_mini });

        Assert.Equal("gpt-5.4-mini", request.Model);
    }

    [Fact]
    public void Model_Falls_Back_To_The_Default_When_Unset()
    {
        var request = ChatMappings.MapOptions(OneMessage(), new ChatOptions());

        Assert.Equal("gpt-5.4-mini", request.Model);
    }

    [Fact]
    public void Messages_Are_Converted_To_Provider_Messages()
    {
        List<OuroMessage> messages =
        [
            OuroMessage.FromSystem("sys"),
            OuroMessage.FromUser("usr")
        ];

        var request = ChatMappings.MapOptions(messages, new ChatOptions());

        Assert.Equal(2, request.Messages.Count);
        Assert.Equal("sys", request.Messages[0].Content);
        Assert.Equal("usr", request.Messages[1].Content);
    }

    [Fact]
    public void Reasoning_Effort_Defaults_To_Medium_For_Reasoning_Models()
    {
        var options = new ChatOptions { Model = OuroModels.Gpt_5_4, ReasoningEffort = null };

        var request = ChatMappings.MapOptions(OneMessage(), options);

        Assert.Equal("medium", request.ReasoningEffort?.ToString());
    }

    [Fact]
    public void Explicit_Reasoning_Effort_Is_Respected()
    {
        var options = new ChatOptions { Model = OuroModels.Gpt_5_4, ReasoningEffort = OuroReasoningEffort.High };

        var request = ChatMappings.MapOptions(OneMessage(), options);

        Assert.Equal("high", request.ReasoningEffort?.ToString());
    }

    [Fact]
    public void No_Schema_Is_Attached_When_ResponseType_Is_Null()
    {
        var request = ChatMappings.MapOptions(OneMessage(), new ChatOptions { ResponseType = null });

        Assert.Null(request.ResponseFormat);
    }

    [Fact]
    public void Schema_Is_Attached_When_ResponseType_Is_Set()
    {
        var options = new ChatOptions { ResponseType = typeof(SampleResult) };

        var request = ChatMappings.MapOptions(OneMessage(), options);

        Assert.NotNull(request.ResponseFormat);
        Assert.Equal(nameof(SampleResult), request.ResponseFormat!.JsonSchema!.Name);
    }

    /// <summary>
    /// The handler used to write the generated schema back onto the caller's ChatOptions, which
    /// leaked state across calls that reused one options instance. It is built at map time now.
    /// </summary>
    [Fact]
    public void Mapping_Does_Not_Mutate_The_Callers_Options()
    {
        var options = new ChatOptions { Model = OuroModels.Gpt_5_4, ResponseType = typeof(SampleResult) };

        ChatMappings.MapOptions(OneMessage(), options);

        Assert.Null(options.ReasoningEffort);
        Assert.Equal(typeof(SampleResult), options.ResponseType);
    }

    private class SampleResult
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
