using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Tracking;

namespace Ouroboros.Test.Tracking;

/// <summary>
/// Covers what the OnChatCompleted hook receives, and what happens when it fails. The Model
/// member in particular is what lets a consumer pick the right tokenizer, so it has to carry the
/// model that actually ran.
/// </summary>
public class ChatCompletedArgsTests
{
    [Fact]
    public async Task Hook_Receives_The_Explicitly_Requested_Model()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")], new ChatOptions { Model = OuroModels.Gpt_5_5 });

        Assert.NotNull(captured);
        Assert.Equal(OuroModels.Gpt_5_5, captured!.Model);
    }

    /// <summary>
    /// The case a future refactor breaks silently: with no model on the options, the hook must
    /// still report the model the request actually ran on, not null or default.
    /// </summary>
    [Fact]
    public async Task Hook_Receives_The_Resolved_Default_When_No_Model_Was_Set()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.NotNull(captured);
        Assert.Equal(Constants.DefaultChatModel, captured!.Model);
    }

    [Fact]
    public async Task Hook_Receives_The_Model_Set_Via_SetDefaultChatModel()
    {
        var (client, _) = BuildClient();
        client.SetDefaultChatModel(OuroModels.Gpt_5_nano, OuroReasoningEffort.High);

        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.NotNull(captured);
        Assert.Equal(OuroModels.Gpt_5_nano, captured!.Model);
        Assert.Equal(OuroReasoningEffort.High, captured.ReasoningEffort);
    }

    [Fact]
    public async Task Hook_Receives_The_Messages_That_Were_Sent()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        List<OuroMessage> messages = [OuroMessage.FromSystem("sys"), OuroMessage.FromUser("usr")];
        await client.ChatAsync(messages);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Messages.Count);
        Assert.Equal(OuroRole.System, captured.Messages[0].Role);
        Assert.Equal("usr", captured.Messages[1].Content);
    }

    /// <summary>
    /// The reason the guard exists: the hook is awaited inline, so before 5.0.0-beta.2 a logging
    /// failure surfaced to the caller as a failed AI call. Log is the default policy.
    /// </summary>
    [Fact]
    public async Task A_Throwing_Hook_Does_Not_Fail_The_Chat_By_Default()
    {
        var (client, _) = BuildClient();
        client.OnChatCompleted = _ => throw new InvalidOperationException("logging blew up");

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.True(response.Success);
        Assert.Equal("stubbed", response.ResponseText);
    }

    [Fact]
    public void The_Default_Policy_Is_Log()
    {
        var (client, _) = BuildClient();

        Assert.Same(HookFailurePolicy.Log, client.OnChatCompletedFailure);
    }

    [Fact]
    public async Task Policy_Log_Writes_To_The_Logger_And_Returns_The_Response()
    {
        var logger = new CapturingLogger();
        var client = new OuroClient("test-key", new StubChatRequestHandler(), logger);
        client.OnChatCompleted = _ => throw new InvalidOperationException("logging blew up");

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.True(response.Success);
        Assert.Single(logger.Errors);
        Assert.IsType<InvalidOperationException>(logger.Errors[0]);
    }

    /// <summary>
    /// The opt-in for consumers whose hook does something the caller genuinely depends on.
    /// </summary>
    [Fact]
    public async Task Policy_Throw_Propagates_The_Original_Exception()
    {
        var (client, _) = BuildClient();
        var thrown = new InvalidOperationException("logging blew up");
        client.OnChatCompleted = _ => throw thrown;
        client.OnChatCompletedFailure = HookFailurePolicy.Throw;

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ChatAsync([OuroMessage.FromUser("hi")]));

        Assert.Same(thrown, caught);
    }

    [Fact]
    public async Task Policy_Ignore_Does_Not_Touch_The_Logger()
    {
        var logger = new CapturingLogger();
        var client = new OuroClient("test-key", new StubChatRequestHandler(), logger);
        client.OnChatCompleted = _ => throw new InvalidOperationException("logging blew up");
        client.OnChatCompletedFailure = HookFailurePolicy.Ignore;

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.True(response.Success);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task Policy_Handle_Receives_The_Exception_And_The_Args()
    {
        var (client, _) = BuildClient();
        var thrown = new InvalidOperationException("logging blew up");
        client.OnChatCompleted = _ => throw thrown;

        Exception? reported = null;
        ChatCompletedArgs? reportedArgs = null;
        client.OnChatCompletedFailure = HookFailurePolicy.Handle((ex, args) =>
        {
            reported = ex;
            reportedArgs = args;
        });

        await client.ChatAsync([OuroMessage.FromUser("hi")], new ChatOptions { PromptName = "my-prompt" });

        Assert.Same(thrown, reported);

        // The args come through so the report can name what failed, not just that something did.
        Assert.NotNull(reportedArgs);
        Assert.Equal("my-prompt", reportedArgs!.PromptName);
    }

    /// <summary>
    /// A throwing handler must not re-break the call the guard just protected, and the failure
    /// still has to land somewhere.
    /// </summary>
    [Fact]
    public async Task Policy_Handle_Falls_Back_To_The_Logger_When_The_Handler_Throws()
    {
        var logger = new CapturingLogger();
        var client = new OuroClient("test-key", new StubChatRequestHandler(), logger);
        client.OnChatCompleted = _ => throw new InvalidOperationException("logging blew up");

        var handlerFailure = new InvalidOperationException("reporting blew up too");
        client.OnChatCompletedFailure = HookFailurePolicy.Handle((_, _) => throw handlerFailure);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.True(response.Success);
        Assert.Same(handlerFailure, Assert.Single(logger.Errors));
    }

    /// <summary>
    /// Covers the async path specifically - a faulted Task rather than a synchronous throw.
    /// </summary>
    [Fact]
    public async Task A_Hook_Returning_A_Faulted_Task_Is_Reported()
    {
        var (client, _) = BuildClient();
        client.OnChatCompleted = _ => Task.FromException(new InvalidOperationException("async blew up"));

        Exception? reported = null;
        client.OnChatCompletedFailure = HookFailurePolicy.Handle((ex, _) => reported = ex);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.True(response.Success);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public async Task The_Failure_Policy_Is_Not_Consulted_When_The_Hook_Succeeds()
    {
        var (client, _) = BuildClient();
        client.OnChatCompleted = _ => Task.CompletedTask;

        var called = false;
        client.OnChatCompletedFailure = HookFailurePolicy.Handle((_, _) => called = true);

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.False(called);
    }

    /// <summary>
    /// The Action&lt;Exception&gt; overload, for handlers that don't care which chat failed.
    /// </summary>
    [Fact]
    public async Task Policy_Handle_Supports_A_Handler_That_Ignores_The_Args()
    {
        var (client, _) = BuildClient();
        var thrown = new InvalidOperationException("logging blew up");
        client.OnChatCompleted = _ => throw thrown;

        Exception? reported = null;
        client.OnChatCompletedFailure = HookFailurePolicy.Handle(ex => reported = ex);

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.Same(thrown, reported);
    }

    private static (OuroClient Client, StubChatRequestHandler Handler) BuildClient()
    {
        var handler = new StubChatRequestHandler();

        return (new OuroClient("test-key", handler), handler);
    }

    /// <summary>
    /// Captures what HookFailurePolicy.Log writes, so the fallback path can be asserted rather
    /// than assumed.
    /// </summary>
    private class CapturingLogger : ILogger<OuroClient>
    {
        public List<Exception> Errors { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception != null)
                Errors.Add(exception);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    /// <summary>
    /// Returns a canned response so OuroClient can be exercised without a network call.
    /// </summary>
    private class StubChatRequestHandler() : ChatRequestHandler(null)
    {
        public override Task<OuroResponseBase> CompleteAsync(List<OuroMessage> messages, OpenAIService api,
            ChatOptions? options = null)
        {
            return Task.FromResult<OuroResponseBase>(new OuroResponseSuccess("stubbed")
            {
                Model = "stub",
                PromptTokens = 11,
                CompletionTokens = 7,
                TotalTokenUsage = 18
            });
        }
    }
}
