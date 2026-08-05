using System;
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;
using Xunit.Abstractions;

namespace Ouroboros.Test.Live;

/// <summary>
/// Real calls against the Anthropic API. Skipped unless ANTHROPIC_API_KEY is set.
/// </summary>
/// <remarks>
/// Everything else in this suite runs against a stub transport, which proves the mapping but not
/// that the mapping is <em>right</em> - a request Anthropic rejects looks identical to one it
/// accepts until you send it. These are the tests that close that gap, so they are worth the money
/// and the seconds they cost.
///
/// They are ordered deliberately: plain chat first, so that when code execution fails you already
/// know whether the provider works at all.
/// </remarks>
public class AnthropicLiveTests(ITestOutputHelper output)
{
    /// <summary>
    /// The cheapest possible proof that the request shape is accepted: auth, model id, the system
    /// prompt lifted to its own field, and a response that maps back to blocks.
    /// </summary>
    [RequiresAnthropicKeyFact]
    public async Task A_Plain_Chat_Round_Trips()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [
                OuroMessage.FromSystem("Answer with a single word and no punctuation."),
                OuroMessage.FromUser("What is the capital of France?")
            ],
            new ChatOptions { Model = OuroModels.Claude_Opus_5, MaxCompletionTokens = 2048 });

        output.WriteLine($"stop={GetStopReason(response)} text={response.ResponseText}");

        AssertSucceeded(response);
        Assert.Contains("Paris", response.ResponseText, StringComparison.OrdinalIgnoreCase);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.NotEmpty(success.Content);
        Assert.True(success.IsComplete, $"Expected a complete turn, got {success.StopReason}.");

        // Local token counting refuses for Claude, so the provider's own numbers are the only
        // source there is. If these come back zero, cost tracking is silently broken.
        Assert.True(success.PromptTokens > 0, "PromptTokens was not reported.");
        Assert.True(success.CompletionTokens > 0, "CompletionTokens was not reported.");
    }

    /// <summary>
    /// The point of the whole exercise: the model writes Python, the provider runs it, and the
    /// output comes back as a block rather than as prose the model claims it produced.
    /// </summary>
    [RequiresAnthropicKeyFact]
    public async Task Code_Execution_Runs_Python_And_Returns_Its_Output()
    {
        using var client = Build();

        // 2 ** 100. Chosen because the answer is exact and unambiguous - the first version of this
        // test asked for a standard deviation, which has two defensible answers (population vs
        // sample) and duly failed against a correct result.
        const string expected = "1267650600228229401496703205376";

        var response = await client.ChatAsync(
            [OuroMessage.FromUser(
                "Using the code execution tool, compute 2**100 and print only the number.")],
            new ChatOptions
            {
                Model = OuroModels.Claude_Opus_5,
                ServerTools = OuroServerTools.CodeExecution,
                MaxCompletionTokens = 8192,

                // Provider-side execution is slow enough that a conventional HTTP default would
                // cut it off. This is exactly what the per-attempt budget exists for.
                Timeout = TimeSpan.FromMinutes(5)
            });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        foreach (var block in success.Content)
            output.WriteLine($"block: {block.Kind}");

        var execution = Assert.Single(success.CodeExecutions);

        output.WriteLine($"code:\n{execution.Code}");
        output.WriteLine($"stdout: {execution.Result?.Stdout}");
        output.WriteLine($"stderr: {execution.Result?.Stderr}");
        output.WriteLine($"text: {success.ResponseText}");

        Assert.NotNull(execution.Result);
        Assert.False(execution.Result!.IsToolError, $"Tool error: {execution.Result.Stderr}");
        Assert.Equal(0, execution.Result.ExitCode);
        Assert.Contains(expected, execution.Result.Stdout);

        // The code the model ran is worth capturing - without it a failed execution is just a
        // stderr string with no way to see what produced it.
        Assert.False(string.IsNullOrWhiteSpace(execution.Code), "The executed code was not captured.");

        Assert.DoesNotContain("Traceback", execution.Result.Stdout);
    }

    /// <summary>
    /// A task big enough that the model writes a script rather than using a one-liner.
    /// </summary>
    /// <remarks>
    /// The regression test for a bug a simpler prompt could never have caught. The code execution
    /// tool has a file-editor half as well as a shell half: asked for anything non-trivial, the
    /// model calls `create` to write a script and then bash to run it, producing two invocations
    /// with two different result shapes. Handling only the bash one left the create invocation in
    /// the response with a null Result - visible, but useless.
    /// </remarks>
    [RequiresAnthropicKeyFact]
    public async Task Every_Execution_In_A_Multi_Step_Task_Comes_Back_With_A_Result()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromUser(
                "Using the code execution tool, write a Python script to a file that seeds the "
                + "random module with 42, generates 20 integers from 1 to 100, and prints their "
                + "mean. Then run the file.")],
            new ChatOptions
            {
                Model = OuroModels.Claude_Opus_5,
                ServerTools = OuroServerTools.CodeExecution,
                MaxCompletionTokens = 8192,
                Timeout = TimeSpan.FromMinutes(5)
            });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);
        var executions = success.CodeExecutions.ToList();

        output.WriteLine($"{executions.Count} execution(s)");

        foreach (var execution in executions)
            output.WriteLine($"  code={execution.Code} exit={execution.Result?.ExitCode}");

        Assert.NotEmpty(executions);

        // The actual assertion: no orphans. Every invocation the model made must have had its
        // result matched back to it, whichever half of the tool produced it.
        Assert.All(executions, execution =>
            Assert.True(execution.Result is not null,
                $"Execution '{execution.Code}' came back with no result attached."));

        // And nothing unrecognised slipped through as an unknown block.
        Assert.DoesNotContain(success.Content, block => block.Kind == OuroBlockKind.Unknown);
    }

    /// <summary>
    /// Cancellation has to reach the provider, not just abandon the await. Otherwise a cancelled
    /// request keeps running - and keeps being billed - on Anthropic's side.
    /// </summary>
    [RequiresAnthropicKeyFact]
    public async Task A_Cancelled_Call_Throws_Rather_Than_Returning()
    {
        using var client = Build();
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ChatAsync(
            [OuroMessage.FromUser("Write a long essay about the sea.")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 },
            cts.Token));
    }

    private static OuroClient Build()
    {
        return new OuroClient(new OuroborosOptions
        {
            AnthropicApiKey = RequiresAnthropicKeyFactAttribute.ApiKey
        });
    }

    /// <summary>
    /// Fails with the provider's own message rather than a bare "expected true".
    /// </summary>
    private static void AssertSucceeded(OuroResponseBase response)
    {
        if (response is OuroResponseFailure failure)
            Assert.Fail($"{failure.ErrorOrigin} {failure.ErrorCode}: {failure.ErrorDetails} | {failure.ResponseText}");

        Assert.True(response.Success);
    }

    private static string GetStopReason(OuroResponseBase response)
    {
        return response is OuroResponseSuccess success ? success.StopReason.ToString() : "n/a";
    }
}
