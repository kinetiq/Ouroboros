using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.Responses;

namespace Ouroboros.Test.Options;

/// <summary>
/// Covers where a request's model comes from, and who is allowed to decide it.
/// </summary>
public class OptionResolutionTests
{
    /// <summary>
    /// ChatAsync used to assign defaults onto the caller's own object, so a reused ChatOptions kept
    /// the first call's model forever and a later SetDefaultChatModel silently never applied.
    /// </summary>
    [Fact]
    public async Task ChatAsync_Does_Not_Write_Into_The_Callers_Options()
    {
        var provider = new RecordingProvider();
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, _ => provider);

        var options = new ChatOptions();

        await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        // The caller's instance is untouched - it never asked for a model.
        Assert.Null(options.Model);

        // But the request still ran against the resolved default.
        Assert.Equal(Constants.DefaultChatModel, provider.LastOptions!.Model);
    }

    /// <summary>
    /// The consequence that actually bit: the same options object reused after the default changes.
    /// </summary>
    [Fact]
    public async Task A_Reused_Options_Object_Picks_Up_A_Changed_Default()
    {
        var provider = new RecordingProvider();
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, _ => provider);

        var options = new ChatOptions();

        await client.ChatAsync([OuroMessage.FromUser("hi")], options);
        Assert.Equal(Constants.DefaultChatModel, provider.LastOptions!.Model);

        client.SetDefaultChatModel(OuroModels.Gpt_5_nano);
        await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        Assert.Equal(OuroModels.Gpt_5_nano, provider.LastOptions!.Model);
    }

    [Fact]
    public async Task An_Explicit_Model_Still_Wins()
    {
        var provider = new RecordingProvider();
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, _ => provider);

        await client.ChatAsync(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_5 });

        Assert.Equal(OuroModels.Gpt_5_5, provider.LastOptions!.Model);
    }

    /// <summary>
    /// Clone is hand-written, so a new property silently stops being copied unless something
    /// checks. Reflection over the public surface is that something.
    /// </summary>
    /// <remarks>
    /// The guard only bites for properties this initializer sets to something other than their
    /// default: an uncopied property compares null to null and passes. So a new property has to be
    /// added <b>here, with a non-default value</b>, not only to Clone. FallbackModels and
    /// AllowDegraded went in without that and were unguarded until a review noticed - a dropped
    /// FallbackModels would have silently swapped a per-call chain for the client default.
    /// </remarks>
    [Fact]
    public void Clone_Copies_Every_Property()
    {
        var original = new ChatOptions
        {
            PromptName = "p",
            Variables = new() { ["k"] = "v" },
            MaxCompletionTokens = 42,
            StopSequences = ["END"],
            Model = OuroModels.Claude_Opus_5,
            ResponseType = typeof(string),
            ReasoningEffort = OuroReasoningEffort.High,
            UseExponentialBackOff = false,
            Timeout = TimeSpan.FromSeconds(7),
            ServerTools = OuroServerTools.CodeExecution,
            Attachments = [new OuroFileRef("file_1") { Provider = OuroProvider.Anthropic }],
            FallbackModels = [OuroModels.Claude_Opus_5],
            AllowDegraded = true,
            OpenAi = { User = "u" }
        };

        var clone = original.Clone();

        AssertEveryPropertyCopied(original, clone, typeof(ChatOptions), requireProperties: true);
    }

    /// <summary>
    /// The provider blocks are copied, not shared.
    /// </summary>
    /// <remarks>
    /// They are mutable settings objects the library may one day resolve defaults into, which is
    /// exactly what Clone exists to keep out of the caller's instance - ChatAsync used to write
    /// straight onto the object it was handed, so a reused ChatOptions kept the first call's model
    /// forever. Sharing the blocks would reintroduce that on the provider surface.
    /// </remarks>
    [Fact]
    public void Clone_Copies_The_Provider_Blocks_Rather_Than_Sharing_Them()
    {
        var original = new ChatOptions { OpenAi = { User = "u" } };
        var clone = original.Clone();

        Assert.NotSame(original.OpenAi, clone.OpenAi);
        Assert.NotSame(original.Anthropic, clone.Anthropic);

        clone.OpenAi.User = "someone-else";

        Assert.Equal("u", original.OpenAi.User);
    }

    /// <summary>
    /// Reflection over the public surface, recursing into the provider blocks so a property added
    /// to one of them is covered by the same guard.
    /// </summary>
    /// <param name="requireProperties">
    /// Guards against the reflection finding nothing and the test passing vacuously. Only applied at
    /// the root: a provider block with no properties yet is a legitimate state, and Anthropic's is
    /// exactly that today.
    /// </param>
    private static void AssertEveryPropertyCopied(object original, object clone, Type type,
        bool requireProperties = false)
    {
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead)
            .ToList();

        if (requireProperties)
            Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var expected = property.GetValue(original);
            var actual = property.GetValue(clone);

            // The blocks are deliberately distinct instances, so compare their contents instead of
            // the references - otherwise this guard would demand the sharing it exists to forbid.
            if (property.PropertyType == typeof(OpenAiChatOptions)
                || property.PropertyType == typeof(AnthropicChatOptions))
            {
                Assert.NotNull(expected);
                Assert.NotNull(actual);
                AssertEveryPropertyCopied(expected!, actual!, property.PropertyType);
                continue;
            }

            Assert.True(Equals(expected, actual),
                $"Clone did not copy {property.Name}: expected {expected ?? "null"}, got {actual ?? "null"}.");
        }
    }

    /// <summary>
    /// A setting in a provider's block reaches that provider's request.
    /// </summary>
    /// <remarks>
    /// The converse - that Anthropic's mapper cannot see it - is deliberately not asserted here,
    /// because it is not a runtime property to assert. AnthropicMappings has no reference to
    /// options.OpenAi and would not compile if it grew one, which is a stronger guarantee than any
    /// test could give. That is the point of moving User off the shared surface: it used to sit
    /// where both mappers could read it, and Anthropic quietly dropped it with nothing in the
    /// response to say a setting had been ignored.
    /// </remarks>
    [Fact]
    public void A_Setting_In_A_Provider_Block_Reaches_That_Providers_Request()
    {
        var request = OpenAiMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Gpt_5_4_mini,
            OpenAi = { User = "end-user-42" }
        });

        Assert.Equal("end-user-42", request.EndUserId);
    }

    /// <summary>
    /// The blocks are never null, so callers can set into them without a null check and mappers can
    /// read them without one either.
    /// </summary>
    [Fact]
    public void Provider_Blocks_Are_Never_Null()
    {
        var options = new ChatOptions();

        Assert.NotNull(options.OpenAi);
        Assert.NotNull(options.Anthropic);
        Assert.NotNull(options.Clone().OpenAi);
        Assert.NotNull(options.Clone().Anthropic);
    }

    /// <summary>
    /// The Anthropic mapper used to fall back to Constants.DefaultChatModel - a GPT model - which
    /// would have stamped "gpt-5.4-mini" onto an Anthropic request and 404d at runtime.
    /// </summary>
    [Fact]
    public void The_Anthropic_Mapper_Refuses_An_Unresolved_Model()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AnthropicMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions { Model = null }));

        Assert.Contains("must be resolved", ex.Message);
    }

    [Fact]
    public void The_OpenAi_Mapper_Refuses_An_Unresolved_Model()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenAiMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions { Model = null }));

        Assert.Contains("must be resolved", ex.Message);
    }

    /// <summary>
    /// Captures what the executor actually handed the provider, without a network call.
    /// </summary>
    private sealed class RecordingProvider : IChatProvider
    {
        public ChatOptions? LastOptions { get; private set; }

        public OuroProvider Kind => OuroProvider.OpenAi;

        public Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
            CancellationToken cancellationToken)
        {
            LastOptions = options;

            return Task.FromResult(ProviderAttempt.Final(new OuroResponseSuccess("ok")));
        }
    }
}
