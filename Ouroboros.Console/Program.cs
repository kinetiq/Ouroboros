using Ouroboros;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Spectre.Console;

AnsiConsole.MarkupLine("[red]Starting...[/]");

var client = new OuroClient("[secret]");

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
