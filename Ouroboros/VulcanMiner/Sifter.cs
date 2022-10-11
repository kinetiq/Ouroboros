using Ouroboros.Document;
using Ouroboros.Scales;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ouroboros.Document.Extensions;

namespace Ouroboros.VulcanMiner;

internal class Sifter
{
    public async Task<List<string>> Mine(string rawData)
    {
        var results = new List<string>();

        var attempts = 0;
        var insights = 0;

        do
        {
            attempts++;

            var fragment = await GenerateInsight(rawData);
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

    /// <summary>
    /// Run a series of requests to gather and validate research ideas.
    /// </summary>
    private async Task<DeepFragment> GenerateInsight(string text)
    {
        var fragment = new DeepFragment(text);

        fragment.AddText("\n\n[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?\n");
        await fragment.ResolveAndSubmit(newElementName: "insight");

        // Citations
        fragment.AddText("\n\n[CITATIONS] From the data above, provide citations that best support or prove the insight.\n1.");
        await fragment.ResolveAndSubmit(newElementName: "citations");

        // Validation
        fragment.AddText("\n\n[VALIDATION] Do you agree that these citations exist in the data and sufficiently justify this insight? Answer using only these words: Strongly Disagree, Disagree, Agree, Strongly Agree\n.");
        await fragment.ResolveAndSubmit(newElementName: "validation");

        return fragment;
    }
}