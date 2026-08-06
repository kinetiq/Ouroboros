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
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, provider);

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
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, provider);

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
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" }, provider);

        await client.ChatAsync(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_5 });

        Assert.Equal(OuroModels.Gpt_5_5, provider.LastOptions!.Model);
    }

    /// <summary>
    /// Clone is hand-written, so a new property silently stops being copied unless something
    /// checks. Reflection over the public surface is that something.
    /// </summary>
    [Fact]
    public void Clone_Copies_Every_Property()
    {
        var original = new ChatOptions
        {
            PromptName = "p",
            Variables = new() { ["k"] = "v" },
            MaxCompletionTokens = 42,
            StopSequences = ["END"],
            User = "u",
            Model = OuroModels.Claude_Opus_5,
            ResponseType = typeof(string),
            ReasoningEffort = OuroReasoningEffort.High,
            UseExponentialBackOff = false,
            Timeout = TimeSpan.FromSeconds(7),
            ServerTools = OuroServerTools.CodeExecution,
            Attachments = [new OuroFileRef("file_1") { Provider = OuroProvider.Anthropic }]
        };

        var clone = original.Clone();

        var properties = typeof(ChatOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead)
            .ToList();

        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var expected = property.GetValue(original);
            var actual = property.GetValue(clone);

            Assert.True(Equals(expected, actual),
                $"Clone did not copy {property.Name}: expected {expected ?? "null"}, got {actual ?? "null"}.");
        }
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
            ChatMappings.MapOptions([OuroMessage.FromUser("hi")], new ChatOptions { Model = null }));

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
