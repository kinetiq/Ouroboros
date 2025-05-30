using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.MarkupLine("[red]Starting...[/]");

var client = new OuroClient("[SECRET]");

var messages = new List<ChatMessage>()
{
    ChatMessage.FromSystem("# Character creator\nCome up with a character name and description based on the Story Seed."),
    ChatMessage.FromUser("Story Seed: A magical giant returns from the cloud to ravage Ireland. Give me a Name and Description for the giant.")
};

var options = new ChatOptions
{
    Model = OuroModels.Gpt_4o,
    ResponseType = typeof(TestType)
};

var response = await client.ChatAsync(messages, options);

Console.WriteLine("Success: " + response.Success);
Console.WriteLine(response.ResponseText);

if (response is OuroResponseSuccess success)
{
    var r = (TestType)success.ResponseObject!;

    Console.WriteLine("Response Object:");
    Console.WriteLine("Name: " + r.Name);
    Console.WriteLine("Description: " + r.Description);
}



public class TestType
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

//var dialog = client.CreateDialog();



//await dialog
//    .SystemMessage("# Writer\r\n" +
//                   "You are a brilliant writer who creates and refines scenes for sci-fi stories.")
//    .UserMessage("Generate 2 great story ideas.")
//    .SendAndAppend() 
//    .UserMessage("From this list, identify the story idea that will bring the most joy to the world. Create an outline for it using the 3-act structure.")
//    .SendAndAppend()
//    .UserMessage("Generate 2 ideas for a main character, numbered. Only include the numbered list. Use no additional commentary.\r\n" +
//                 "Format: [Number]. [Name]: [Summary]")
//    .SendAndAppend()
//    .Execute();

//if (dialog.HasErrors)
//{
//    Console.WriteLine("Last Error: " + dialog.LastError);
//}

//Console.WriteLine(dialog.ToString());

//// Reusable dialogs. 
//await dialog.UserMessage("Which of those ideas is best?")
//    .SendAndAppend()
//    .Execute();

//Console.WriteLine(dialog.ToString());
