using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ouroboros.Document;
using Ouroboros.Document.Elements;
using Ouroboros.Document.Extensions;
using Ouroboros.OpenAI;
using Ouroboros.Scales;

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

    private async Task<DeepFragment> GenerateInsight(string text)
    {
        var fragment = new DeepFragment(text);

        fragment.AddText("\n\n[INSIGHT] Based on this data, what is a fascinating insight that is worthy of further research?\n");
        await fragment.ResolveAndSubmit(newElementName: "insight");

        // Citations
        fragment.AddText("\n\n[CITATIONS] From the data above, provide three citations that prove our insight comes from the data, or is reinforced by it.\n1.");
        await fragment.ResolveAndSubmit(newElementName: "citations");

        // Validation
        fragment.AddText("\n\n[VALIDATION] Do the citations exist in the data and sufficiently justify this insight? Answer using only the words: Strongly Disagree, Disagree, Agree Strongly Agree\n.");
        await fragment.ResolveAndSubmit(newElementName: "validation");

        return fragment;
    }

    public async Task<List<string>> Mine(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);
        var results = new List<string>();
 
        var attempts = 0;
        var insights = 0;

        do
        {
            attempts++;

            var fragment = await GenerateInsight(text);
            var likert = fragment.GetLastAsLikert();

            if (likert >= LikertAgreement4.Agree)
            {
                var insight = fragment.GetById("insight");
                results.Add(insight.ToString());

                insights++;
            }
        } while (insights < 3 && attempts < 10);

        return results;
    }
}