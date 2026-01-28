using Ouroboros.Chaining;
using Ouroboros.Enums;
using Ouroboros.Extensions;

namespace Ouroboros.AutoMiner;

public class Miner(OuroClient ouroClient)
{
    public async Task<List<string>> MineAsync(string rawData, int insightGoal = 1, int maxAttempts = 10)
    {
        var results = new List<string>();

        var attempts = 0;
        var insights = 0;

        do
        {
            attempts++;

            var doc = await GenerateInsightAsync(rawData);

            var likert = doc.GetLastAsLikert();

            if (likert < LikertAgreement4.Agree)
                continue;

            var insight = doc.Variables["insight"];
            results.Add(insight);

            var citation = doc.Variables["citations"];
            results.Add("citations:");
            results.Add(citation);

            //insights++;
        } while (insights < insightGoal && attempts < maxAttempts);

        // TODO: go further - can we propose ways to research this? Sources to explore? 

        return results;
    }

    /// <summary>
    /// Run a series of requests to gather and validate research ideas.
    /// </summary>
    private async Task<Dialog> GenerateInsightAsync(string text)
    {
        var dialog = ouroClient.CreateDialog();

       var response = await dialog.SystemMessage(text)
            .UserMessage("[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?")
            .SendAndAppend()
            .StoreOutputAs("insight")
            .UserMessage("[CITATIONS] Using the data above, provide a few quotes that best support or prove the insight.")
            .SendAndAppend()
            .StoreOutputAs("citations")
            .UserMessage("[VALIDATION] Do you agree that these quotes exist in the data and sufficiently justify this insight? To qualify, the quotes must clearly exist in the data above. Answer using only these words: Strongly Disagree, Disagree, Agree, Strongly Agree")
            .SendAndAppend()
            .StoreOutputAs("validation")
            .Execute();

        if (!response.Success)
            throw new InvalidOperationException("Failed: " + response.ResponseText);

        return dialog;
    }
}