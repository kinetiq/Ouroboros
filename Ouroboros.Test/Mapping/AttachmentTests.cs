using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Models.Messages;
using OpenAI.Responses;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Mapping;

/// <summary>
/// Covers attaching uploaded files to a request, and refusing the shapes a provider would accept
/// but silently ignore.
/// </summary>
public class AttachmentTests
{
    private static readonly OuroFileRef Csv =
        new("file_abc123") { Provider = OuroProvider.Anthropic, FileName = "data.csv" };

    private static readonly OuroFileRef OpenAiCsv =
        new("file-xyz789") { Provider = OuroProvider.OpenAi, FileName = "data.csv" };

    [Fact]
    public void With_No_Attachments_Content_Stays_A_Plain_String()
    {
        var mapped = Map([OuroMessage.FromUser("hello")], attachments: null);

        var message = Assert.Single(mapped.Messages);

        Assert.True(message.Content.TryPickString(out var text));
        Assert.Equal("hello", text);
    }

    /// <summary>
    /// The files hang off the last user turn: that is the request they are evidence for, and it
    /// keeps them next to the question rather than stranded at the top of a long conversation.
    /// </summary>
    [Fact]
    public void An_Attachment_Becomes_A_Container_Upload_Block_On_The_Last_User_Turn()
    {
        var mapped = Map([OuroMessage.FromUser("Summarise this.")], [Csv]);

        var message = Assert.Single(mapped.Messages);

        Assert.True(message.Content.TryPickContentBlockParams(out var blocks));
        Assert.Equal(2, blocks!.Count);

        // The question survives - the file is extra context, not a replacement for it.
        Assert.True(blocks[0].TryPickText(out var text));
        Assert.Equal("Summarise this.", text!.Text);

        Assert.True(blocks[1].TryPickContainerUpload(out var upload));
        Assert.Equal("file_abc123", upload!.FileID);
    }

    [Fact]
    public void Earlier_Turns_Are_Left_Alone()
    {
        var mapped = Map(
            [
                OuroMessage.FromUser("First question."),
                OuroMessage.FromAssistant("An answer."),
                OuroMessage.FromUser("Now use the file.")
            ],
            [Csv]);

        Assert.Equal(3, mapped.Messages.Count);

        // Only the final user turn carries blocks; the first is still a plain string.
        Assert.True(mapped.Messages[0].Content.TryPickString(out _));
        Assert.True(mapped.Messages[2].Content.TryPickContentBlockParams(out var blocks));
        Assert.Equal(2, blocks!.Count);
    }

    [Fact]
    public void Several_Attachments_All_Come_Through_In_Order()
    {
        var second = new OuroFileRef("file_def456") { Provider = OuroProvider.Anthropic };

        var mapped = Map([OuroMessage.FromUser("Compare these.")], [Csv, second]);

        Assert.True(mapped.Messages[0].Content.TryPickContentBlockParams(out var blocks));

        var ids = blocks!
            .Where(block => block.TryPickContainerUpload(out _))
            .Select(block => { block.TryPickContainerUpload(out var upload); return upload!.FileID; })
            .ToList();

        Assert.Equal(["file_abc123", "file_def456"], ids);
    }

    /// <summary>
    /// A container_upload block only means something inside a code execution container. Sent
    /// without one it is accepted and ignored, and the model answers as though the file was never
    /// mentioned - which reads as the model being obtuse rather than the request being wrong.
    /// </summary>
    [Fact]
    public async Task Attachments_Without_Code_Execution_Are_Refused()
    {
        var response = await Send(new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            Attachments = [Csv]
        });

        var error = Assert.IsType<OuroResponseInternalError>(response);
        Assert.Contains("CodeExecution", error.ResponseText);
    }

    [Fact]
    public async Task An_Attachment_From_Another_Provider_Is_Refused()
    {
        var foreignFile = new OuroFileRef("file_openai") { Provider = OuroProvider.OpenAi };

        var response = await Send(new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            ServerTools = OuroServerTools.CodeExecution,
            Attachments = [foreignFile]
        });

        var error = Assert.IsType<OuroResponseInternalError>(response);

        // Names both sides, so the fix is obvious rather than requiring a guess.
        Assert.Contains("OpenAi", error.ResponseText);
        Assert.Contains("not portable", error.ResponseText);
    }

    /// <summary>
    /// A ResponseType now reaches Anthropic as a schema rather than being refused.
    /// </summary>
    /// <remarks>
    /// This test used to assert the opposite. It was right to: while the mapper ignored ResponseType
    /// the request went out unconstrained, the response failed to parse, and the caller got a null
    /// ResponseObject on an otherwise successful call - indistinguishable from a model that returned
    /// nothing useful. The refusal existed to make that visible, and is obsolete now the schema is
    /// actually sent.
    /// </remarks>
    [Fact]
    public void A_ResponseType_On_Anthropic_Becomes_An_Output_Schema()
    {
        var mapped = AnthropicMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            ResponseType = typeof(SampleShape)
        });

        Assert.NotNull(mapped.OutputConfig);
        Assert.NotNull(mapped.OutputConfig!.Format);
        Assert.Contains("Name", mapped.OutputConfig.Format!.Schema["properties"].ToString());
    }

    /// <summary>
    /// No ResponseType and no effort means no output config at all, rather than an empty one.
    /// </summary>
    [Fact]
    public void No_ResponseType_Leaves_The_Output_Config_Off()
    {
        var mapped = AnthropicMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5
        });

        Assert.Null(mapped.OutputConfig);
    }

    private sealed record SampleShape(string Name);

    /// <summary>
    /// An OpenAI attachment is mounted into the interpreter's container.
    /// </summary>
    [Fact]
    public void An_OpenAi_Attachment_Becomes_A_Container_File()
    {
        var mapped = OpenAiMappings.MapOptions([OuroMessage.FromUser("Summarise this.")], new ChatOptions
        {
            Model = OuroModels.Gpt_5_4_mini,
            ServerTools = OuroServerTools.CodeExecution,
            Attachments = [OpenAiCsv]
        });

        var tool = Assert.IsType<CodeInterpreterTool>(Assert.Single(mapped.Tools));
        var configuration = Assert.IsType<AutomaticCodeInterpreterToolContainerConfiguration>(
            tool.Container.ContainerConfiguration);

        Assert.Equal([OpenAiCsv.Id], configuration.FileIds);
    }

    /// <summary>
    /// A reference issued by one provider is refused by the other, before a call is spent.
    /// </summary>
    /// <remarks>
    /// File ids are opaque and provider-scoped. Sent to the wrong vendor the request would come back
    /// as a baffling 404 about an id the caller can see plainly exists.
    ///
    /// This test used to assert that OpenAI refused attachments outright, because they were not
    /// implemented. It kept passing once they were - on the Anthropic reference it happened to use -
    /// which is exactly the sort of test that reads as coverage while asserting something else.
    /// </remarks>
    [Fact]
    public async Task An_Anthropic_Attachment_Sent_To_OpenAi_Is_Refused_And_Spends_Nothing()
    {
        var transport = new StubTransport(StubTransport.Response("ignored"));

        var response = await new ChatExecutor().ExecuteAsync(
            new OpenAiResponsesProvider(transport.ToClient()),
            [OuroMessage.FromUser("Summarise this.")],
            new ChatOptions
            {
                Model = OuroModels.Gpt_5_4_mini,
                ServerTools = OuroServerTools.CodeExecution,
                Attachments = [Csv]
            });

        var error = Assert.IsType<OuroResponseInternalError>(response);

        Assert.Contains("Anthropic", error.ResponseText);
        Assert.Contains(Csv.Id, error.ResponseText);
        Assert.Equal(0, transport.Calls);
    }

    /// <summary>
    /// Attachments without the tool that mounts them are refused on OpenAI too - the rule is
    /// Ouroboros' own, so it cannot differ by provider.
    /// </summary>
    [Fact]
    public async Task An_OpenAi_Attachment_Without_Code_Execution_Is_Refused()
    {
        var transport = new StubTransport(StubTransport.Response("ignored"));

        var response = await new ChatExecutor().ExecuteAsync(
            new OpenAiResponsesProvider(transport.ToClient()),
            [OuroMessage.FromUser("Summarise this.")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini, Attachments = [OpenAiCsv] });

        Assert.IsType<OuroResponseInternalError>(response);
        Assert.Contains("CodeExecution", response.ResponseText);
        Assert.Equal(0, transport.Calls);
    }

    private static MessageCreateParams Map(List<OuroMessage> messages, IReadOnlyList<OuroFileRef>? attachments)
    {
        return AnthropicMappings.MapOptions(messages, new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            Attachments = attachments
        });
    }

    /// <summary>
    /// Runs the provider far enough to hit validation. The rejections happen before any network
    /// call, so no transport is needed - and a stub that was never reached proves it.
    /// </summary>
    private static async Task<OuroResponseBase> Send(ChatOptions options)
    {
        var provider = new AnthropicChatProvider(new global::Anthropic.AnthropicClient { ApiKey = "test-key" });

        var attempt = await provider.SendAsync(
            [OuroMessage.FromUser("Summarise this.")], options, CancellationToken.None);

        // A rejection is final by definition - retrying a malformed request just repeats it.
        Assert.False(attempt.IsRetryable);

        return attempt.Response;
    }
}
