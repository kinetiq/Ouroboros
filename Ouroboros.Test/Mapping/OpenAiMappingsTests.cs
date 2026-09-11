using System.Collections.Generic;
using OpenAI.Responses;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;

namespace Ouroboros.Test.Mapping;

/// <summary>
/// The OpenAI half of what AnthropicMappingsTests covers. Effort is the part worth pinning: the
/// installed SDK names only three of the five levels, so two arms build the wire value from a
/// string and nothing but a test would notice a typo.
/// </summary>
public class OpenAiMappingsTests
{
    [Theory]
    [InlineData(OuroReasoningEffort.Low, "low")]
    [InlineData(OuroReasoningEffort.Medium, "medium")]
    [InlineData(OuroReasoningEffort.High, "high")]
    [InlineData(OuroReasoningEffort.XHigh, "xhigh")]
    [InlineData(OuroReasoningEffort.Max, "max")]
    public void Reasoning_Effort_Is_Carried_As_A_Reasoning_Option(OuroReasoningEffort level, string expected)
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Gpt_6_Astra,
            ReasoningEffort = level
        });

        Assert.NotNull(mapped.ReasoningOptions);
        Assert.Equal(expected, mapped.ReasoningOptions!.ReasoningEffortLevel.ToString());
    }

    /// <summary>
    /// The bug the casts in MapEffort exist to prevent. ResponseReasoningEffortLevel converts
    /// implicitly from string, so an uncast switch ran the null arm through that conversion and
    /// threw on every reasoning call that named no effort.
    /// </summary>
    [Fact]
    public void No_Reasoning_Effort_Means_No_Reasoning_Options()
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Gpt_6_Astra,
            ReasoningEffort = null
        });

        Assert.Null(mapped.ReasoningOptions);
    }

    [Theory]
    [InlineData(OuroModels.Gpt_5_5, "gpt-5.5")]
    [InlineData(OuroModels.Gpt_5_6_Sol, "gpt-5.6-sol")]
    [InlineData(OuroModels.Gpt_5_6_Terra, "gpt-5.6-terra")]
    [InlineData(OuroModels.Gpt_5_6_Luna, "gpt-5.6-luna")]
    [InlineData(OuroModels.Gpt_6_Astra, "gpt-6-astra")]
    public void The_Model_Id_Reaches_The_Request(OuroModels model, string expected)
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions { Model = model });

        Assert.Equal(expected, mapped.Model);
    }

    private static CreateResponseOptions Map(List<OuroMessage> messages, ChatOptions? options = null)
    {
        return OpenAiMappings.MapOptions(messages, options ?? new ChatOptions
        {
            Model = OuroModels.Gpt_6_Astra
        });
    }
}
