using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ouroboros.Document;
using Ouroboros.Document.Extensions;
using Ouroboros.Scales;
using Ouroboros.VulcanMiner;

[assembly: InternalsVisibleToAttribute("Ouroboros.Test")]

namespace Ouroboros;

public class Client
{
    //private string TemplateRoot { get; set; }

    public async Task<string> Resolve(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);

        var fragment = new DeepFragment(text);
        await fragment.Resolve();

        return fragment.ToString();
    }

    public async Task<string> Summarize(string text, int maxSentences)
    {
        var fragment = new DeepFragment("text");
        await fragment.Resolve();

        return fragment.ToString();
    }

    public async Task<List<string>> Mine(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);
        var sifter = new Sifter();

        return await sifter.Mine(text);
    }
}