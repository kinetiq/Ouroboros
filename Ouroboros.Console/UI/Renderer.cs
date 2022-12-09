using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Documents;
using Spectre.Console;

namespace Ouroboros.Console.UI;

internal static class Renderer
{
    internal static void Render(Document document)
    {
        foreach (var element in document.DocElements)
        {

            AnsiConsole.MarkupLine("[green]" + element.Type() + "[/]");

            if (document.LastResolvedElement != null && document.LastResolvedElement == element)
                AnsiConsole.Markup("[red]" + element + "[/]");
            else
                AnsiConsole.WriteLine(element.ToString());
        }
    }
}