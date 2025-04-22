using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds a user message to the chat context.
/// </summary>
internal class AddUserMessage : IChatCommand
{
    public string Text { get; set; }
    public string ElementName { get; set; }

    public OuroMessage ToOuroMessage() => new(ChatMessage.FromUser(Text), ElementName);

    public AddUserMessage(string text, string elementName = "")
    {
        Text = text;
        ElementName = elementName;
    }
}