﻿using OpenAI.ObjectModels.RequestModels;
using Ouroboros;
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


//var client = new OuroClient("[SECRET]");

//var xx = new List<string>()
//{
//    "Positive, improve delegation",
//    "Negative, improve communication"
//};

//var test = await client.RequestEmbeddings(xx);


//AnsiConsole.Markup(test.ResponseText);

//if (test is EmbeddingResponseSuccess success)
//{
//    foreach (var item in success.Embeddings)
//    {
//        AnsiConsole.MarkupLine(item.ToString());
//    }
//}

//var v1 = test.Embeddings[0].Embedding;
//var v2 = test.Embeddings[1].Embedding;

//var distance = Ouroboros.Vectors.Calculate.EuclideanDistance(v1, v2);
//var dotProductResult = Ouroboros.Vectors.Calculate.DotProduct(v1, v2);

//AnsiConsole.MarkupLine("Distance: " + distance);
//AnsiConsole.MarkupLine("Dot Product: " + dotProductResult);

var client = new OuroClient("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");
var dialog = client.CreateDialog();

var response = await dialog
    .SystemMessage("# Theologian" + Environment.NewLine +
                   "You are a Jewish theologian who knows everything about religion.")
    .UserMessage("How big is God?")
    .SendAndAppend()
    .UserMessage("And what would Satan say about that?")
    .SendAndAppend()
    .Execute();

if (dialog.HasErrors)
{
    AnsiConsole.MarkupLine("[red]Last Error: " + dialog.LastError + "[/]");
}

AnsiConsole.Markup(dialog.ToString());