using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining;

public class OuroMessage 
{
    internal readonly ChatMessage Message;

    internal string Role => Message.Role;
    internal string Content => Message.Content ?? "";

    internal OuroMessage(ChatMessage message)
    {
        Message = message;
    }
}
