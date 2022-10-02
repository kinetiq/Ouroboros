using Spectre.Console;

// See https://aka.ms/new-console-template for more information
AnsiConsole.Markup("[underline red]Starting...");
AnsiConsole.WriteLine();

var client = new Ouroboros.Client();

var text = await client.Resolve("D:\\GPT\\characters.txt");

AnsiConsole.Markup(text);