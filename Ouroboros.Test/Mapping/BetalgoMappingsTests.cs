using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using BetalgoRole = Betalgo.Ranul.OpenAI.Contracts.Enums.ChatCompletionRole;
using BetalgoEffort = Betalgo.Ranul.OpenAI.Contracts.Enums.ReasoningEffort;

namespace Ouroboros.Test.Mapping;

/// <summary>
/// Covers the Ouroboros -> Betalgo boundary. This is the layer that gets replaced when we
/// swap provider SDKs, so it is worth pinning.
/// </summary>
public class BetalgoMappingsTests
{
    [Fact]
    public void System_Message_Maps_Role_And_Content()
    {
        var mapped = OuroMessage.FromSystem("be helpful").ToBetalgo();

        Assert.Equal(BetalgoRole.System, mapped.Role);
        Assert.Equal("be helpful", mapped.Content);
    }

    [Fact]
    public void User_Message_Maps_Role_And_Content()
    {
        var mapped = OuroMessage.FromUser("hello").ToBetalgo();

        Assert.Equal(BetalgoRole.User, mapped.Role);
        Assert.Equal("hello", mapped.Content);
    }

    [Fact]
    public void Assistant_Message_Maps_Role_And_Content()
    {
        var mapped = OuroMessage.FromAssistant("hi there").ToBetalgo();

        Assert.Equal(BetalgoRole.Assistant, mapped.Role);
        Assert.Equal("hi there", mapped.Content);
    }

    [Fact]
    public void Every_Role_Has_A_Mapping()
    {
        foreach (var role in Enum.GetValues<OuroRole>())
        {
            var mapped = new OuroMessage(role, "x").ToBetalgo();

            Assert.NotNull(mapped.Role);
        }
    }

    [Theory]
    [InlineData(OuroReasoningEffort.Low, "low")]
    [InlineData(OuroReasoningEffort.Medium, "medium")]
    [InlineData(OuroReasoningEffort.High, "high")]
    public void Reasoning_Effort_Maps_To_Betalgo(OuroReasoningEffort effort, string expected)
    {
        BetalgoEffort mapped = effort.ToBetalgo();

        Assert.Equal(expected, mapped.ToString());
    }

    [Fact]
    public void Null_Reasoning_Effort_Stays_Null()
    {
        OuroReasoningEffort? effort = null;

        Assert.Null(effort.ToBetalgo());
    }
}
