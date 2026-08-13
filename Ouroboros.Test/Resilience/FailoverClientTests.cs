using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;
using Ouroboros.Tracking;

namespace Ouroboros.Test.Resilience;

/// <summary>
/// How a client turns options into a chain, refuses one it cannot honour, and reports what ran.
/// </summary>
public class FailoverClientTests
{
    private const OuroModels Gpt = OuroModels.Gpt_5_4_mini;
    private const OuroModels Claude = OuroModels.Claude_Opus_5;

    [Fact]
    public async Task A_Per_Call_Fallback_Runs_When_The_Primary_Is_Exhausted()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic, "claude answered");

        using var client = Build(gpt, claude);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: [Claude]));

        Assert.True(response.Success);
        Assert.Equal("claude answered", response.ResponseText);
        Assert.Equal(1, claude.Calls);
    }

    [Fact]
    public async Task A_Client_Default_Fallback_Applies_To_Calls_That_Say_Nothing()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic, "claude answered");

        using var client = Build(gpt, claude);
        client.SetDefaultFallback(Claude);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], Options());

        Assert.Equal("claude answered", response.ResponseText);
    }

    /// <summary>
    /// An empty list means the caller wants no failover, and beats the client default.
    /// </summary>
    /// <remarks>
    /// The same distinction Model draws between unset and set. Without it there would be no way to
    /// opt a single call out - an eval that must run on one named model, say.
    /// </remarks>
    [Fact]
    public async Task An_Empty_Fallback_List_Opts_Out_Of_The_Client_Default()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);
        client.SetDefaultFallback(Claude);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: []));

        Assert.False(response.Success);
        Assert.Equal(0, claude.Calls);
    }

    [Fact]
    public async Task A_Fallback_Equal_To_The_Primary_Is_Skipped()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);

        using var client = Build(gpt, FakeProvider.Succeeds(OuroProvider.Anthropic));

        await client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: [Gpt]));

        // One entry, so one exhausted retry budget - not two.
        Assert.Equal(1, gpt.Calls);
    }

    /// <summary>
    /// The hook reports every attempt, with the model each one actually ran on.
    /// </summary>
    /// <remarks>
    /// The failed attempt is the point. A consumer logging these gets a row for the chat that was
    /// paid for and failed, and can see from NextModel what happened next - rather than one row that
    /// says the call succeeded on a model it only reached second.
    /// </remarks>
    [Fact]
    public async Task The_Hook_Fires_Once_Per_Attempt_With_The_Model_That_Ran()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);

        var seen = new List<ChatCompletedArgs>();
        client.OnChatCompleted = args => { seen.Add(args); return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: [Claude]));

        Assert.Equal(2, seen.Count);

        Assert.Equal(Gpt, seen[0].Model);
        Assert.Equal(1, seen[0].Attempt);
        Assert.Equal(Claude, seen[0].NextModel);
        Assert.False(seen[0].Response.Success);

        Assert.Equal(Claude, seen[1].Model);
        Assert.Equal(2, seen[1].Attempt);
        Assert.Null(seen[1].NextModel);
        Assert.True(seen[1].Response.Success);
    }

    /// <summary>
    /// Without a fallback the hook behaves exactly as it always has.
    /// </summary>
    [Fact]
    public async Task Without_A_Fallback_The_Hook_Fires_Exactly_Once()
    {
        using var client = Build(FakeProvider.Succeeds(OuroProvider.OpenAi), FakeProvider.Succeeds(OuroProvider.Anthropic));

        var seen = new List<ChatCompletedArgs>();
        client.OnChatCompleted = args => { seen.Add(args); return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")], Options());

        var only = Assert.Single(seen);

        Assert.Equal(1, only.Attempt);
        Assert.Null(only.NextModel);
        Assert.Equal(Gpt, only.Model);
    }

    /// <summary>
    /// A throwing hook does not cost the caller the response of an attempt it never saw.
    /// </summary>
    /// <remarks>
    /// Under HookFailurePolicy.Throw, bailing out on the first hook failure would mean a logging
    /// fault while recording the failed attempt threw away the successful one - a chat the caller
    /// paid for and would never receive. Every hook runs; the first exception is raised afterwards.
    /// </remarks>
    [Fact]
    public async Task A_Throwing_Hook_Still_Reports_The_Remaining_Attempts()
    {
        var gpt = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);
        client.OnChatCompletedFailure = HookFailurePolicy.Throw;

        var seen = new List<int>();

        client.OnChatCompleted = args =>
        {
            seen.Add(args.Attempt);

            if (args.Attempt == 1)
                throw new InvalidOperationException("the logger fell over");

            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: [Claude])));

        Assert.Equal([1, 2], seen);
    }

    /// <summary>
    /// An option the chain cannot carry fails when the call is made, not after the primary succeeds.
    /// </summary>
    [Fact]
    public async Task An_Explicit_Chain_That_Cannot_Carry_An_Option_Fails_Fast()
    {
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);

        var options = Options(fallback: [Gpt]);
        options.Model = Claude;
        options.StopSequences = ["END"];

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        var error = Assert.IsType<OuroResponseInternalError>(response);

        Assert.Contains("StopSequences", error.ResponseText);
        Assert.Contains("AllowDegraded", error.ResponseText);

        // Nothing was spent finding that out.
        Assert.Equal(0, gpt.Calls);
        Assert.Equal(0, claude.Calls);
    }

    /// <summary>
    /// The same conflict inherited from the client default drops the entry instead of failing.
    /// </summary>
    /// <remarks>
    /// This is the difference between a fallback that is a safety net and one that is a trap.
    /// Configuring a client-wide default must not break calls that were working - a Claude call
    /// using stop sequences would otherwise start failing the moment anyone set an OpenAI fallback,
    /// whether or not it ever failed over.
    /// </remarks>
    [Fact]
    public async Task A_Client_Default_Entry_That_Cannot_Carry_An_Option_Is_Dropped()
    {
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic, "claude answered");

        using var client = Build(gpt, claude);
        client.SetDefaultFallback(Gpt);

        var options = Options();
        options.Model = Claude;
        options.StopSequences = ["END"];

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        Assert.Equal("claude answered", response.ResponseText);
        Assert.Equal(0, gpt.Calls);
    }

    /// <summary>
    /// AllowDegraded lets the entry run without the option it cannot serve.
    /// </summary>
    [Fact]
    public async Task AllowDegraded_Strips_The_Option_From_Only_The_Entry_That_Cannot_Serve_It()
    {
        var claude = FakeProvider.AlwaysRetryable(OuroProvider.Anthropic);
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);

        using var client = Build(gpt, claude);

        var options = Options(fallback: [Gpt]);
        options.Model = Claude;
        options.StopSequences = ["END"];
        options.AllowDegraded = true;

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        Assert.True(response.Success);

        // Claude kept them, because Claude can honour them.
        Assert.Equal(["END"], claude.LastOptions?.StopSequences);

        // OpenAI ran without them, which is what was opted into.
        Assert.Null(gpt.LastOptions?.StopSequences);
    }

    /// <summary>
    /// Degradation never drops an attachment, even when the caller asked for it.
    /// </summary>
    /// <remarks>
    /// A model answering about a file it cannot see returns a confident answer to a different
    /// question, and it comes back looking like a success. The entry is dropped instead.
    /// </remarks>
    [Fact]
    public async Task AllowDegraded_Never_Strips_An_Attachment()
    {
        var claude = FakeProvider.AlwaysRetryable(OuroProvider.Anthropic);
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);

        using var client = Build(gpt, claude);

        var options = Options(fallback: [Gpt]);
        options.Model = Claude;
        options.ServerTools = OuroServerTools.CodeExecution;
        options.Attachments = [new OuroFileRef("file_1") { Provider = OuroProvider.Anthropic }];
        options.AllowDegraded = true;

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        // The chain ran out at Claude rather than asking GPT about a file it has no access to. The
        // failure has to be Claude's own - asserting only on Success would pass just as happily if
        // validation had refused the whole call, which is a different behaviour entirely.
        var failure = Assert.IsType<OuroResponseProviderError>(response);

        Assert.Equal(OuroProvider.Anthropic.ToString(), failure.ErrorOrigin);
        Assert.Equal(1, claude.Calls);
        Assert.Equal(0, gpt.Calls);
    }

    /// <summary>
    /// Without AllowDegraded, an explicit chain entry that cannot see the files fails the call.
    /// </summary>
    /// <remarks>
    /// The caller named this chain for this request, so a silent shortening would hide that half of
    /// it was never usable. Telling them is the point; AllowDegraded is how they say they know.
    /// </remarks>
    [Fact]
    public async Task An_Explicit_Chain_Entry_That_Cannot_See_The_Attachments_Fails_Fast()
    {
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);

        var options = Options(fallback: [Gpt]);
        options.Model = Claude;
        options.ServerTools = OuroServerTools.CodeExecution;
        options.Attachments = [new OuroFileRef("file_1") { Provider = OuroProvider.Anthropic }];

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        var error = Assert.IsType<OuroResponseInternalError>(response);

        Assert.Contains("not portable", error.ResponseText);
        Assert.Contains("AllowDegraded", error.ResponseText);
        Assert.Equal(0, claude.Calls);
    }

    /// <summary>
    /// A client-default fallback to a provider with no key is refused where it is declared.
    /// </summary>
    /// <remarks>
    /// It used to be dropped with a log when the call ran. That is silent degradation of the one
    /// feature bought for an emergency: you would find out during the outage. Configuration is known
    /// long before a call, so it is checked long before a call - here at the point of declaration,
    /// and at host startup for the OuroborosOptions path.
    /// </remarks>
    [Fact]
    public void A_Client_Default_Fallback_Without_A_Key_Is_Refused()
    {
        using var client = new OuroClient(
            new OuroborosOptions { OpenAiApiKey = "test" },
            _ => FakeProvider.Succeeds(OuroProvider.OpenAi));

        var ex = Assert.Throws<InvalidOperationException>(() => client.SetDefaultFallback(Claude));

        Assert.Contains("Anthropic", ex.Message);
        Assert.Contains(nameof(OuroborosOptions.FallbackModels), ex.Message);
    }

    /// <summary>
    /// The same gap named explicitly for this call is an error rather than a silent shortening.
    /// </summary>
    /// <remarks>
    /// Same distinction Validate draws for an option a chain entry cannot carry: a chain the caller
    /// wrote for this call is their intent, so a hole in it is worth telling them about.
    /// </remarks>
    [Fact]
    public async Task An_Explicit_Fallback_Without_A_Key_Is_Refused()
    {
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = new OuroClient(
            new OuroborosOptions { OpenAiApiKey = "test" },
            model => model.GetProvider() == OuroProvider.OpenAi ? gpt : claude);

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], Options(fallback: [Claude]));

        var error = Assert.IsType<OuroResponseInternalError>(response);

        Assert.Contains("Anthropic", error.ResponseText);
        Assert.Contains("API key", error.ResponseText);

        // A response, not an exception - and nothing was spent discovering it.
        Assert.Equal(0, gpt.Calls);
        Assert.Equal(0, claude.Calls);
    }

    /// <summary>
    /// A request that is wrong however it is routed fails, and never spends a call.
    /// </summary>
    [Fact]
    public async Task An_Invalid_Request_Fails_Rather_Than_Falling_Over()
    {
        var gpt = FakeProvider.Succeeds(OuroProvider.OpenAi);
        var claude = FakeProvider.Succeeds(OuroProvider.Anthropic);

        using var client = Build(gpt, claude);

        var options = Options(fallback: [Claude]);

        // Attachments with no tool to mount them into: nobody can serve this.
        options.Attachments = [new OuroFileRef("file_1") { Provider = OuroProvider.OpenAi }];

        var response = await client.ChatAsync([OuroMessage.FromUser("hi")], options);

        Assert.IsType<OuroResponseInternalError>(response);
        Assert.Contains("CodeExecution", response.ResponseText);
        Assert.Equal(0, gpt.Calls);
        Assert.Equal(0, claude.Calls);
    }

    private static ChatOptions Options(IList<OuroModels>? fallback = null)
    {
        return new ChatOptions
        {
            Model = Gpt,

            // These assert on control flow; the real backoff schedule would add minutes per
            // exhausted entry without changing one outcome.
            UseExponentialBackOff = false,
            FallbackModels = fallback
        };
    }

    private static OuroClient Build(FakeProvider openAi, FakeProvider anthropic)
    {
        return new OuroClient(
            new OuroborosOptions { OpenAiApiKey = "test", AnthropicApiKey = "test" },
            model => model.GetProvider() == OuroProvider.OpenAi ? openAi : anthropic);
    }
}
