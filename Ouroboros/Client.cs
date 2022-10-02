using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ouroboros.Document;
using Ouroboros.OpenAI;

namespace Ouroboros;

public class Client
{
    //private string TemplateRoot { get; set; }

    public async Task<string> Resolve(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);

        var fragment = new DeepFragment(text);
        var doc = await fragment.Resolve();

        return doc.ToString();
    }
}