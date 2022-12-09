using Ouroboros.Document;
using Ouroboros.Scales;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ouroboros.Document.Extensions;
using Ouroboros.OpenAI;

namespace Ouroboros.VulcanMiner;

internal class Sifter
{
    private readonly OpenAiClient Client;

    public async Task<List<string>> Mine(string rawData, int insightGoal = 1, int maxAttempts = 10)
    {
        var results = new List<string>();

        var attempts = 0;
        var insights = 0;

        do
        {
            attempts++;

            var doc = await GenerateInsight(rawData);
            var likert = doc.GetLastAsLikert();

            if (likert >= LikertAgreement4.Agree)
            {
                var insight = doc.GetById("insight");
                results.Add(insight.ToString());

                insights++;
            }
        } while (insights < insightGoal && attempts < maxAttempts);

        // TODO: return both the insight and the citations.
        // TODO: go further - can we propose ways to research this? Sources to explore? 
        // TODO: maybe we can say that after researching it against all human knowledge, I discovered... 

        // TODO: maybe build a genius researcher. Maybe we could stage a debate between some of the best minds in history on the topic.

        return results;
    }

    /// <summary>
    /// Run a series of requests to gather and validate research ideas.
    /// </summary>
    private async Task<Document.Document> GenerateInsight(string text)
    {
        var fragment = new Document.Document(Client, text);

        fragment.AddText("\n\n[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?\n");
        await fragment.ResolveAndSubmit(newElementName: "insight");

        // TODO: it might be better to ask for multiple insights off the bat, and then:
        // TODO: Split them into a list. 
        // TODO: Re-run what follows for each. 

        // Citations
        fragment.AddText("\n\n[CITATIONS] From the data above, provide citations that best support or prove the insight.\n1.");
        await fragment.ResolveAndSubmit(newElementName: "citations");

        // Validation
        fragment.AddText("\n\n[VALIDATION] Do you agree that these citations exist in the data and sufficiently justify this insight? Answer using only these words: Strongly Disagree, Disagree, Agree, Strongly Agree\n.");
        await fragment.ResolveAndSubmit(newElementName: "validation");

        return fragment;
    }

    internal Sifter(OpenAiClient client)
    {
        Client = client;
    }
}