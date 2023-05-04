using Ouroboros;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.Completions;
using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.MarkupLine("[red]Starting...[/]");

//var client = new OuroClient("[SECRET]");
//var miner = new Miner(client);

//var text = await File.ReadAllTextAsync(@"D:\GPT\data.txt");
//var results = await miner.MineAsync(text, 1);

//foreach (var result in results)
//{
//    AnsiConsole.MarkupLine("insight:");
//    AnsiConsole.MarkupLine(result);
//}


//var document = await client.ResolveNext(@"D:\GPT\kevin.txt");
//Renderer.Render(document);

//await client.ResolveNext(document);

//AnsiConsole.MarkupLine("[red]Next...[/]");
//Renderer.Render(document);




//foreach (var element in document.DocElements)
//{

//    AnsiConsole.MarkupLine("[green]" + element.Type() + "[/]");

//    if (document.LastResolvedElement != null && document.LastResolvedElement == element)
//        AnsiConsole.Markup("[red]" + element + "[/]");
//    else 
//        AnsiConsole.WriteLine(element.ToString());
//}




//foreach (var item in output)
//{
//    AnsiConsole.WriteLine(item);
//    AnsiConsole.WriteLine();
//}


var client = new OuroClient("[VALUE]");

var options = new CompleteOptions()
{
    Model = OuroModels.Gpt_4
};

var response = await client.PromptToStringAsync(
    "John is such a jerk. Just last week, we went to lunch and " +
    "he refused to pay, that wanker. That has happened so many times. But " +
    "professionally, he's just not a great communicator. I don't like working with him" +
    "but he does seem to get results. That's why Bob keeps him around.", options);


AnsiConsole.Markup(response.ResponseText);