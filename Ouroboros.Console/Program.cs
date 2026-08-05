using System.Text;
using System.IO;
using System.Linq;
using Ouroboros;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Spectre.Console;

AnsiConsole.MarkupLine("[red]Starting...[/]");

// Keys come from the environment rather than being pasted here - this file is committed, and a
// key in it would be too. Set whichever you want to exercise:
//   setx OPENAI_API_KEY    "..."
//   setx ANTHROPIC_API_KEY "..."
// setx only affects new processes, so open a fresh terminal afterwards.
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

var client = new OuroClient(new OuroborosOptions
{
    OpenAiApiKey = openAiKey,
    AnthropicApiKey = anthropicKey
});

// --- Claude + provider-side code execution -------------------------------
// Claude writes Python, Anthropic runs it, and the output comes back as its own block rather
// than as prose the model claims it produced. Edit the prompt and re-run to poke at it.

if (!string.IsNullOrWhiteSpace(anthropicKey))
{
    AnsiConsole.MarkupLine("\n[yellow]-- code execution --[/]");

    var codeResponse = await client.ChatAsync(
        [OuroMessage.FromUser(
            "Using the code execution tool, generate 20 random integers between 1 and 100 with a "
            + "fixed seed, then print their mean, median and standard deviation.")],
        new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            ServerTools = OuroServerTools.CodeExecution,
            MaxCompletionTokens = 8192,

            // Server-side execution runs well past a conventional HTTP default.
            Timeout = TimeSpan.FromMinutes(5)
        });

    if (codeResponse is OuroResponseSuccess codeSuccess)
    {
        // Model output goes through Markup.Escape - it is arbitrary text, and Spectre reads square
        // brackets as markup tags. A printed list like [82, 14] otherwise crashes the renderer.
        foreach (var execution in codeSuccess.CodeExecutions)
        {
            AnsiConsole.MarkupLine($"[grey]code:[/]\n{Markup.Escape(execution.Code ?? "")}");
            AnsiConsole.MarkupLine($"[grey]exit:[/] {execution.Result?.ExitCode}");
            AnsiConsole.MarkupLine($"[grey]stdout:[/]\n{Markup.Escape(execution.Result?.Stdout ?? "")}");

            if (!string.IsNullOrWhiteSpace(execution.Result?.Stderr))
                AnsiConsole.MarkupLine($"[red]stderr:[/]\n{Markup.Escape(execution.Result.Stderr)}");
        }

        // Note what is NOT in here: stdout stays in its block, so this is only the model's prose.
        AnsiConsole.MarkupLine($"[grey]text:[/]\n{Markup.Escape(codeSuccess.ResponseText)}");
        AnsiConsole.MarkupLine(
            $"[grey]stop:[/] {codeSuccess.StopReason} | complete: {codeSuccess.IsComplete} | " +
            $"tokens: {codeSuccess.PromptTokens} in / {codeSuccess.CompletionTokens} out");
    }

    if (codeResponse is OuroResponseFailure codeFailure)
        AnsiConsole.MarkupLine($"[red]Failure:[/] {codeFailure.ErrorOrigin} | {codeFailure.ResponseText}");

    // --- File round trip -------------------------------------------------
    // Data in, analysis, artifact out. The sandbox has no internet, so the Files API is the only
    // way across that boundary.

    AnsiConsole.MarkupLine("\n[yellow]-- file round trip --[/]");

    const string csv = "name,score\nada,37\ngrace,41\nalan,29\nedsger,53\n";

    var upload = await client.UploadFileAsync(
        Encoding.UTF8.GetBytes(csv), "scores.csv", "text/csv", OuroProvider.Anthropic);

    AnsiConsole.MarkupLine($"[grey]uploaded:[/] {upload.Id}");

    try
    {
        var fileResponse = await client.ChatAsync(
            [OuroMessage.FromUser(
                "Read the attached CSV with the code execution tool, print the mean score, then "
                + "save a bar chart of it as a PNG.")],
            new ChatOptions
            {
                Model = OuroModels.Claude_Opus_5,
                ServerTools = OuroServerTools.CodeExecution,
                Attachments = [upload],
                MaxCompletionTokens = 8192,
                Timeout = TimeSpan.FromMinutes(5)
            });

        if (fileResponse is OuroResponseSuccess fileSuccess)
        {
            foreach (var generated in fileSuccess.CodeExecutions.SelectMany(x => x.Result?.Files ?? []))
            {
                var content = await client.DownloadFileAsync(generated);
                var path = Path.Combine(Path.GetTempPath(), content.FileName ?? $"{generated.Id}.bin");

                await File.WriteAllBytesAsync(path, content.Content);

                AnsiConsole.MarkupLine($"[green]saved:[/] {Markup.Escape(path)} ({content.SizeBytes} bytes)");
            }

            AnsiConsole.MarkupLine($"[grey]text:[/]\n{Markup.Escape(fileSuccess.ResponseText)}");
        }

        if (fileResponse is OuroResponseFailure fileFailure)
            AnsiConsole.MarkupLine($"[red]Failure:[/] {fileFailure.ErrorOrigin} | {fileFailure.ResponseText}");
    }
    finally
    {
        // Uploads persist until deleted and count against the account's storage.
        await client.DeleteFileAsync(upload);
    }
}
else
{
    AnsiConsole.MarkupLine("[grey]ANTHROPIC_API_KEY not set - skipping the code execution demo.[/]");
}

if (string.IsNullOrWhiteSpace(openAiKey))
{
    AnsiConsole.MarkupLine("[grey]OPENAI_API_KEY not set - stopping before the OpenAI sections.[/]");
    return;
}

// --- Simple chat ---------------------------------------------------------

var messages = new List<OuroMessage>()
{
    OuroMessage.FromSystem("# Character creator\nCome up with a character name and description based on the Story Seed."),
    OuroMessage.FromUser("Story Seed: A magical giant returns from the cloud to ravage Ireland. Give me a Name and Description for the giant")
};

var options = new ChatOptions
{
    MaxCompletionTokens = 20,
    ReasoningEffort = OuroReasoningEffort.High,
    Model = OuroModels.Gpt_5_4_mini,
};

var response = await client.ChatAsync(messages, options);

if (response is OuroResponseSuccess success)
{
    Console.WriteLine(success.ResponseText);
}

if (response is OuroResponseFailure failure)
{
    Console.WriteLine($"Failure: {failure.ErrorOrigin} | {failure.ResponseText} {failure.ErrorCode} ");
}

// --- Chaining ------------------------------------------------------------
// Exercises the fluent path end to end. This has no automated coverage, so it is the
// smoke test for anything that touches Dialog.

var dialog = client.CreateDialog();

await dialog
    .SystemMessage("# Writer\r\n" +
                   "You are a brilliant writer who creates and refines scenes for sci-fi stories.")
    .UserMessage("Generate 2 great story ideas.")
    .SendAndAppend()
    .UserMessage("From this list, identify the story idea that will bring the most joy to the world. Create an outline for it using the 3-act structure.")
    .SendAndAppend()
    .UserMessage("Generate 2 ideas for a main character, numbered. Only include the numbered list. Use no additional commentary.\r\n" +
                 "Format: [Number]. [Name]: [Summary]")
    .SendAndAppend()
    .Execute();

if (dialog.HasErrors)
{
    Console.WriteLine("Last Error: " + dialog.LastError);
}

Console.WriteLine(dialog.ToString());

// Dialogs are reusable - this continues the same conversation.
await dialog.UserMessage("Which of those ideas is best?")
    .SendAndAppend()
    .Execute();

Console.WriteLine(dialog.ToString());
