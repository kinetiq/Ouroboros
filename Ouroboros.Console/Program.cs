using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.Markup("Starting...");
AnsiConsole.WriteLine();


var client = new Ouroboros.Client();

var output = await client.Mine(@"D:\GPT\data.txt");

foreach (var item in output)
{
    AnsiConsole.WriteLine(item);
    AnsiConsole.WriteLine();
}

//var text = await client.Resolve("D:\\GPT\\characters.txt");



//AnsiConsole.Markup(text);