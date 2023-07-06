using System.Reflection.Metadata;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using Ouroboros.Scales;

namespace Ouroboros.AutoMiner;

public class Miner
{
    private readonly OuroClient OuroClient;

    public async Task<List<string>> MineAsync(string rawData, int insightGoal = 1, int maxAttempts = 10)
    {
        var results = new List<string>();

        var attempts = 0;
        var insights = 0;

        do
        {
            attempts++;

            return new List<string>() { "Done" };

            //var doc = await GenerateInsightAsync(rawData);

            //var likert = doc.GetLastAsLikert();

            //if (likert < LikertAgreement4.Agree)
            //    continue;

            //var insight = doc.GetByName("insight");
            //results.Add(insight.ToString());

            //var citation = doc.GetByName("citations");
            //results.Add("citations:");
            //results.Add(citation.ToString());

            //insights++;
        } while (insights < insightGoal && attempts < maxAttempts);

        // TODO: go further - can we propose ways to research this? Sources to explore? 
        // TODO: maybe we can say that after researching it against all human knowledge, I discovered... 
        
        // TODO: maybe build a genius researcher. Maybe we could stage a debate between some of the best minds in history on the topic.

        return results;
    }

    /// <summary>
    /// Run a series of requests to gather and validate research ideas.
    /// </summary>
    private async Task<Document> GenerateInsightAsync(string text)
    {
        return new Document();

        var chat = new List<ChatMessage>()
        {
            ChatMessage.FromSystem(text),
            ChatMessage.FromUser("[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?"),
        };

        var r1 = await OuroClient.ChatAsync(chat);

        // chat  = OurClient.CreateChat(); // ChatContext knows if anything resulted in an error, and has a reference to the client.
        // dialog = chat
        //      .SetSystem("..."),
        //      .FromUser("...", "p1")
        //      .Ask() //  SendAndAppend(), SendToString(), AskToLikert()     
        //      .AddUserMessage("...");
        //      
        // if (chat.HasErrors) { ... }
        //
        // dialog.RemoveStartingAt("p1");

        // Chat.SetSystem("...")
        //     .SetUser("...", "elementName")
        //     .CompleteAnd();

        //var response = await OuroClient
        //    .Prompt(text)
        //    .Chain("\n\n[INSIGHT] Based on this data, what is a clever insight that is worthy of further research?\r\n", newElementName: "insight")
        //    .Chain("\n\n[CITATIONS] Using the data above, provide a few quotes that best support or prove the insight. \r\n1.", newElementName: "citations")
        //    .Chain("\n\n[VALIDATION] Do you agree that these quotes exist in the data and sufficiently justify this insight? To qualify, the quotes must clearly exist in the data above. Answer using only these words: Strongly Disagree, Disagree, Agree, Strongly Agree\n.", newElementName: "validation")
        //    .CompleteToDocumentAsync();

        //if (!response.Success)
        //    throw new InvalidOperationException("Failed: " + response.CompleteResponse.ResponseText);

        //return response.Value!;
    }

    public Miner(OuroClient ouroClient)
    {
        OuroClient = ouroClient;
    }
}