using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Live;

/// <summary>
/// Real calls against OpenAI's Responses API. Skipped unless OPENAI_API_KEY is set.
/// </summary>
/// <remarks>
/// The sibling of AnthropicLiveTests, and doubly worth having. This provider replaced a working one
/// built on a different SDK against a different wire API. Every stub test in the suite asserts the
/// mapper produces the shape we believe in, and that belief is what changed. Only a real call can
/// say whether OpenAI agrees.
/// </remarks>
public class OpenAiLiveTests(ITestOutputHelper output)
{
    private const OuroModels Model = OuroModels.Gpt_5_4_mini;

    /// <summary>
    /// The cheapest possible proof the request shape is accepted: auth, model id, the system prompt
    /// lifted to Instructions, and a response that maps back to blocks.
    /// </summary>
    /// <remarks>
    /// Note there is no ReasoningEffort here. That is deliberate - every OpenAI model in OuroModels
    /// carries [Reasoning], so an unset effort takes the nullable path through the effort mapper,
    /// which is precisely where the first version of this provider threw ArgumentNullException on
    /// every single call.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task A_Plain_Chat_Round_Trips()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [
                OuroMessage.FromSystem("Answer with a single word and no punctuation."),
                OuroMessage.FromUser("What is the capital of France?")
            ],
            new ChatOptions { Model = Model, MaxCompletionTokens = 2048 });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        output.WriteLine($"stop={success.StopReason} text={success.ResponseText}");

        Assert.Contains("Paris", success.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(success.Content);
        Assert.True(success.IsComplete, $"Expected a complete turn, got {success.StopReason}.");

        Assert.True(success.PromptTokens > 0, "PromptTokens was not reported.");
        Assert.True(success.CompletionTokens > 0, "CompletionTokens was not reported.");
    }

    /// <summary>
    /// A conversation with no user turn still round-trips.
    /// </summary>
    /// <remarks>
    /// The exact shape that broke in 5.0: the Instructions lift left 'input' empty and OpenAI
    /// returned 400 missing_required_parameter. The mapper now sends system-only conversations as
    /// system-role input items, and only a real call proves OpenAI accepts that form.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task A_System_Only_Chat_Round_Trips()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromSystem("You reply with a single word. The word is: Paris. Reply now.")],
            new ChatOptions { Model = Model, MaxCompletionTokens = 2048 });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        output.WriteLine($"stop={success.StopReason} text={success.ResponseText}");

        Assert.Contains("Paris", success.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.True(success.IsComplete, $"Expected a complete turn, got {success.StopReason}.");
    }

    /// <summary>
    /// What model string the API echoes back.
    /// </summary>
    /// <remarks>
    /// Not a formality. Consumers key their pricing tables on this exact string. Keystone stores it
    /// on every chat row and joins it to a model table to compute cost.
    ///
    /// If Responses echoes a different shape than Chat Completions did, the join misses, a priceless
    /// row is created, and cost goes null with nothing failing anywhere. So the string is asserted
    /// present and recognisable, and printed for comparison against what is already stored.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task The_Echoed_Model_Id_Is_Reported()
    {
        using var client = Build();

        // Both current models rather than just the default. They are separately deployed, so a
        // convention holding for one is evidence about the other and not proof.
        foreach (var model in new[] { OuroModels.Gpt_5_4_mini, OuroModels.Gpt_5_4 })
        {
            var requested = ModelMappings.GetModelNameAsString(model);

            var response = await client.ChatAsync(
                [OuroMessage.FromUser("Say OK.")],
                new ChatOptions { Model = model, MaxCompletionTokens = 2048 });

            AssertSucceeded(response);

            var success = Assert.IsType<OuroResponseSuccess>(response);

            output.WriteLine($"requested: {requested}");
            output.WriteLine($"echoed:    {success.Model}");

            Assert.False(string.IsNullOrWhiteSpace(success.Model),
                $"No model id came back for {requested}.");

            // Dated ids like gpt-5.4-mini-2026-03-17 are expected and fine - what would not be is a
            // string unrelated to what was asked for, which is what breaks the pricing join.
            Assert.StartsWith(requested, success.Model, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Structured output end to end: the generated schema is accepted, the model honours it, and the
    /// result deserializes into the caller's type.
    /// </summary>
    /// <remarks>
    /// This is the load-bearing one for the schema generator. Its output is only ever checked
    /// against our own expectations in the unit tests; strict mode means OpenAI validates it too,
    /// and rejects the request outright if it is malformed. A schema that is well-formed but wrongly
    /// cased would pass validation and still null every field, which is why the values are asserted
    /// rather than just the shape.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task Structured_Output_Returns_A_Typed_Object()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromUser("Ada Lovelace was born in 1815 in London.")],
            new ChatOptions
            {
                Model = Model,
                ResponseType = typeof(Person),
                MaxCompletionTokens = 4096
            });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        output.WriteLine($"text: {success.ResponseText}");

        var person = Assert.IsType<Person>(success.ResponseObject);

        output.WriteLine($"parsed: {person.Name} / {person.BirthYear} / {person.BirthCity}");

        Assert.Contains("Ada", person.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1815, person.BirthYear);
        Assert.Contains("London", person.BirthCity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// On a structured call, ResponseText is the raw JSON.
    /// </summary>
    /// <remarks>
    /// Pinned separately because consumers persist it verbatim as their durable record - eval
    /// history, structurizer output, chat logs - and read it back later. If a provider swap started
    /// storing prose there instead, nothing would break today and every stored row from then on
    /// would be unparseable.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task Structured_Output_Leaves_The_Raw_Json_In_ResponseText()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromUser("Grace Hopper was born in 1906 in New York.")],
            new ChatOptions
            {
                Model = Model,
                ResponseType = typeof(Person),
                MaxCompletionTokens = 4096
            });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        output.WriteLine($"text: {success.ResponseText}");

        // Parsed independently of ResponseObject: this asserts the stored string is itself valid
        // JSON of the right shape, not merely that the library managed to parse something.
        var reparsed = JsonSerializer.Deserialize<Person>(success.ResponseText);

        Assert.NotNull(reparsed);
        Assert.Equal(1906, reparsed!.BirthYear);
    }

    /// <summary>
    /// A response cut off at the token ceiling is reported as such rather than passing for complete.
    /// </summary>
    /// <remarks>
    /// The stub test pins the mapping from an incomplete status; this pins that OpenAI actually
    /// reports one. A reasoning model spends the budget on tokens we never see, so a low ceiling is
    /// reliably hit - which is the point, but it also means Content may well be empty.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task A_Truncated_Response_Reports_MaxTokens()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromUser("Write a detailed history of the Byzantine empire.")],
            new ChatOptions { Model = Model, MaxCompletionTokens = 16 });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        output.WriteLine($"stop={success.StopReason} text={success.ResponseText}");

        Assert.Equal(OuroStopReason.MaxTokens, success.StopReason);
        Assert.False(success.IsComplete);

        // Truncated, not failed. The caller decides what to do about it.
        Assert.True(success.Success);
    }

    /// <summary>
    /// Stop sequences are refused rather than dropped, and nothing is spent doing it.
    /// </summary>
    /// <remarks>
    /// Live rather than stubbed because the claim being made is about the API, not about our code:
    /// there is no stop parameter on Responses. If OpenAI ever adds one, this refusal becomes wrong
    /// - but a stub test would never notice.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task Stop_Sequences_Are_Refused_With_A_Message_Naming_The_Setting()
    {
        using var client = Build();

        var response = await client.ChatAsync(
            [OuroMessage.FromUser("Count to ten.")],
            new ChatOptions { Model = Model, StopSequences = ["5"] });

        var error = Assert.IsType<OuroResponseInternalError>(response);

        output.WriteLine(error.ResponseText);

        Assert.Contains("StopSequences", error.ResponseText);
    }

    /// <summary>
    /// Cancellation has to reach the provider, not just abandon the await - otherwise a cancelled
    /// request keeps running, and keeps being billed, on OpenAI's side.
    /// </summary>
    [RequiresOpenAiKeyFact]
    public async Task A_Cancelled_Call_Throws_Rather_Than_Returning()
    {
        using var client = Build();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ChatAsync(
            [OuroMessage.FromUser("Write a long essay about the sea.")],
            new ChatOptions { Model = Model },
            cts.Token));
    }

    /// <summary>
    /// The model writes Python, OpenAI runs it, and the output comes back as a block rather than as
    /// prose the model claims it produced.
    /// </summary>
    /// <remarks>
    /// 2 ** 100, because the answer is exact and unambiguous. The Anthropic version of this test
    /// originally asked for a standard deviation, which has two defensible answers - population and
    /// sample - and duly failed against a correct result.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task Code_Execution_Runs_Python_And_Returns_Its_Output()
    {
        using var client = Build();

        const string expected = "1267650600228229401496703205376";

        var response = await client.ChatAsync(
            [OuroMessage.FromUser(
                "Using the code interpreter, compute 2**100 and print only the number.")],
            new ChatOptions
            {
                Model = Model,
                ServerTools = OuroServerTools.CodeExecution,
                MaxCompletionTokens = 8192,

                // Provider-side execution is slow enough that a conventional HTTP default would cut
                // it off. This is what the per-attempt budget exists for.
                Timeout = TimeSpan.FromMinutes(5)
            });

        AssertSucceeded(response);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        foreach (var block in success.Content)
            output.WriteLine($"block: {block.Kind}");

        var executions = success.CodeExecutions.ToList();

        Assert.NotEmpty(executions);

        foreach (var execution in executions)
        {
            output.WriteLine($"code: {execution.Code}");
            output.WriteLine($"exit={execution.Result?.ExitCode} stdout={execution.Result?.Stdout}");
        }

        // The outputs arriving at all is the assertion that matters: without the IncludedProperties
        // opt-in on the request they come back empty and the model looks like it ran nothing.
        var stdout = string.Concat(executions.Select(execution => execution.Result?.Stdout));

        Assert.Contains(expected, stdout);
        Assert.All(executions, execution =>
        {
            Assert.NotNull(execution.Result);
            Assert.False(execution.Result!.IsToolError, "The tool itself failed.");
            Assert.Equal(0, execution.Result.ExitCode);
        });

        // The code the model ran is worth capturing - without it a failed execution is just output
        // with no way to see what produced it.
        Assert.Contains(executions, execution => !string.IsNullOrWhiteSpace(execution.Code));
    }

    /// <summary>
    /// The full round trip: data in, analysis, artifact out.
    /// </summary>
    /// <remarks>
    /// This is the half of code execution that makes it useful. On OpenAI it is also the only
    /// exercise of the two-store split: the upload is addressed by id alone, while the chart the
    /// interpreter writes lives inside the container and needs both ids. A reference that lost its
    /// ContainerScope would 404 here and nowhere else.
    ///
    /// Cleans up after itself. Uploads persist and count against the account, so a test leaking one
    /// per run would accumulate forever.
    /// </remarks>
    [RequiresOpenAiKeyFact]
    public async Task A_File_Round_Trips_Through_Code_Execution()
    {
        using var client = Build();

        // Deliberately not round numbers - a mean of 30 could be arrived at without reading the
        // file, whereas this one could not.
        const string csv = """
            name,score
            ada,37
            grace,41
            alan,29
            edsger,53
            """;

        const string expectedMean = "40";

        var upload = await client.UploadFileAsync(
            Encoding.UTF8.GetBytes(csv), "scores.csv", "text/csv", OuroProvider.OpenAi);

        output.WriteLine($"uploaded: {upload.Id} ({upload.FileName})");

        Assert.Equal(OuroProvider.OpenAi, upload.Provider);

        try
        {
            var response = await client.ChatAsync(
                [OuroMessage.FromUser(
                    "Read the attached CSV with the code interpreter. Print the mean of the score "
                    + "column, then save a bar chart of it as a PNG file.")],
                new ChatOptions
                {
                    Model = Model,
                    ServerTools = OuroServerTools.CodeExecution,
                    Attachments = [upload],
                    MaxCompletionTokens = 8192,
                    Timeout = TimeSpan.FromMinutes(5)
                });

            AssertSucceeded(response);

            var success = Assert.IsType<OuroResponseSuccess>(response);
            var executions = success.CodeExecutions.ToList();

            foreach (var execution in executions)
                output.WriteLine($"exit={execution.Result?.ExitCode} stdout={execution.Result?.Stdout}");

            // It read the file we uploaded, not something it invented.
            var stdout = string.Concat(executions.Select(execution => execution.Result?.Stdout));

            Assert.Contains(expectedMean, stdout);

            var generated = executions
                .SelectMany(execution => execution.Result?.Files ?? [])
                .ToList();

            output.WriteLine($"{generated.Count} generated file(s)");

            foreach (var file in generated)
                output.WriteLine($"  {file.Id} {file.FileName}");

            // Not Single: a charting turn routinely yields more than one artifact - the interpreter
            // emits its own rendered image alongside the file the script saved - and demanding
            // exactly one would fail on a correct result.
            Assert.NotEmpty(generated);

            var png = generated.First(file =>
                file.FileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true);

            try
            {
                // Every generated reference has to carry its container. Without one the id is
                // simply not resolvable - container files are a different endpoint from the
                // account-wide store - so a null here is a download that 404s later.
                Assert.All(generated, file => Assert.NotNull(file.ContainerScope));

                var downloaded = await client.DownloadFileAsync(png);

                output.WriteLine($"downloaded {downloaded.SizeBytes} bytes, name={downloaded.FileName}");

                // The PNG magic number. Asserting on the bytes rather than a non-zero length is what
                // proves the reference round-tripped to the right file.
                Assert.True(downloaded.SizeBytes > 1000, $"Suspiciously small: {downloaded.SizeBytes} bytes.");
                Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, downloaded.Content.Take(4));
            }
            finally
            {
                // All of them, not just the one downloaded. The Anthropic version of this test
                // originally cleaned up only the file it asserted on and leaked a chart per run.
                foreach (var file in generated)
                    await client.DeleteFileAsync(file);
            }
        }
        finally
        {
            await client.DeleteFileAsync(upload);
        }
    }

    /// <summary>
    /// Deliberately plain: a string, an int, and a described property, which is the shape almost
    /// every real structured call takes.
    /// </summary>
    private sealed class Person
    {
        public string Name { get; set; } = "";

        [Description("The four-digit year of birth.")]
        public int BirthYear { get; set; }

        public string BirthCity { get; set; } = "";
    }

    private static OuroClient Build()
    {
        return new OuroClient(new OuroborosOptions
        {
            OpenAiApiKey = RequiresOpenAiKeyFactAttribute.ApiKey
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
}
