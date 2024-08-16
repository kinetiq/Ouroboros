using OpenAI.ObjectModels.RequestModels;
using Ouroboros;
using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.MarkupLine("[red]Starting...[/]");

var client = new OuroClient("[SECRET]");
var dialog = client.CreateDialog();

AnsiConsole.MarkupLine("[red]Let's go...[/]");

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

// Reusable dialogs. 
await dialog.UserMessage("Which of those ideas is best?")
    .SendAndAppend()
    .Execute();

Console.WriteLine(dialog.ToString());
