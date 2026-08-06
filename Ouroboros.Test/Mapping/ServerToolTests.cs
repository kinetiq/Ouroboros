using System.Linq;
using System.Threading.Tasks;
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
/// Covers opting in to provider-side tools, and what happens when the provider cannot serve them.
/// </summary>
public class ServerToolTests
{
    [Fact]
    public void No_Tools_Are_Declared_By_Default()
    {
        var mapped = AnthropicMappings.MapOptions(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 });

        // Declaring tools unasked would change both cost and behaviour, so the default has to be
        // nothing at all rather than an empty list the provider still has to parse.
        Assert.Null(mapped.Tools);
    }

    [Fact]
    public void Requesting_Code_Execution_Declares_The_Tool()
    {
        var mapped = AnthropicMappings.MapOptions(
            [OuroMessage.FromUser("Work out the mean.")],
            new ChatOptions
            {
                Model = OuroModels.Claude_Opus_5,
                ServerTools = OuroServerTools.CodeExecution
            });

        Assert.NotNull(mapped.Tools);
        Assert.Single(mapped.Tools!);
    }

    /// <summary>
    /// The failure this is designed to avoid: silently dropping the flag produces a response in
    /// which the model explains it cannot run code, which reads as a model limitation rather than a
    /// configuration mistake and is thoroughly miserable to diagnose.
    /// </summary>
    [Fact]
    public async Task Requesting_Code_Execution_From_OpenAi_Fails_And_Says_Why()
    {
        var transport = new StubTransport(StubTransport.Response("ignored"));

        var response = await new ChatExecutor().ExecuteAsync(
            new OpenAiResponsesProvider(transport.ToClient()),
            [OuroMessage.FromUser("Work out the mean.")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini, ServerTools = OuroServerTools.CodeExecution });

        Assert.IsType<OuroResponseInternalError>(response);

        // Names the capability and points at the fix, rather than just refusing.
        Assert.Contains("CodeExecution", response.ResponseText);
        Assert.Contains("Claude", response.ResponseText);

        // And it fails before spending a request.
        Assert.Equal(0, transport.Calls);
    }
}
