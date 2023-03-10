using Ouroboros.Builder;
using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using Ouroboros.Scales;

namespace Ouroboros.AutoMiner;

public class Sifter
{
    private readonly OuroClient OuroClient;

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

            if (likert < LikertAgreement4.Agree) 
                continue;
            
            var insight = doc.GetByName("insight");
            results.Add(insight.ToString());

            var citation = doc.GetByName("citations");
            results.Add("citations:");
            results.Add(citation.ToString());

            insights++;
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
    private async Task<Document> GenerateInsight(string text)
    {
        return await OuroClient
            .StartChain(text)
            .Chain("\n\n[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?\r\n",  newElementName: "insight")
            .Chain("\n\n[CITATIONS] Using the data above, provide a few quotes that best support or prove the insight. \r\n1.", newElementName: "citations")
            .Chain("\n\n[VALIDATION] Do you agree that these quotes exist in the data and sufficiently justify this insight? To qualify, the quotes must clearly exist in the data above. Answer using only these words: Strongly Disagree, Disagree, Agree, Strongly Agree\n.", newElementName: "validation")
            .AsDocumentAsync();
    }

    public Sifter(OuroClient ouroClient)
    {
        OuroClient = ouroClient;
    }
}