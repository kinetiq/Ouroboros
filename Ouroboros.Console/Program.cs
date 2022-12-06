using Ouroboros.Console.UI;
using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.MarkupLine("[red]Starting...[/]");

var client = new Ouroboros.Client();

var document = await client.ResolveNext(@"D:\GPT\kevin.txt");
Renderer.Render(document);

await client.ResolveNext(document);

AnsiConsole.MarkupLine("[red]Next...[/]");
Renderer.Render(document);


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


//var text = await client.Resolve("D:\\GPT\\characters.txt");

//var text = await client.Summarize(
//    "John is such a dick. Just last week, we went to lunch and " + 
//    "he refused to pay, that wanker. That has happened so many times. But " +
//    "professionally, he's just not a great communicator. I don't like working with him" +
//    "but he does seem to get results. That's why Bob keeps him around.", 2);

//AnsiConsole.Markup(text);