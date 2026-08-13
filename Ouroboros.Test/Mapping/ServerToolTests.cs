using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public void No_Tools_Are_Declared_By_Default_On_OpenAi()
    {
        var mapped = OpenAiMappings.MapOptions(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini });

        Assert.Empty(mapped.Tools);
        Assert.Empty(mapped.IncludedProperties);
    }

    /// <summary>
    /// Opting in declares the tool and asks for its outputs.
    /// </summary>
    /// <remarks>
    /// The IncludedProperties half is the one worth pinning. Without it the request still succeeds
    /// and the tool still runs, but the outputs come back empty - so the model looks like it
    /// executed nothing, and the only symptom is an absence.
    /// </remarks>
    [Fact]
    public void Requesting_Code_Execution_Declares_The_OpenAi_Tool_And_Asks_For_Its_Outputs()
    {
        var mapped = OpenAiMappings.MapOptions(
            [OuroMessage.FromUser("Work out the mean.")],
            new ChatOptions
            {
                Model = OuroModels.Gpt_5_4_mini,
                ServerTools = OuroServerTools.CodeExecution
            });

        Assert.Single(mapped.Tools);
        Assert.IsType<CodeInterpreterTool>(mapped.Tools[0]);
        Assert.Contains(IncludedResponseProperty.CodeInterpreterCallOutputs, mapped.IncludedProperties);
    }
}
