using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds an assistant message to the chat context.
/// </summary>
internal class AddAssistantMessage : IChatCommand
{
    public string Text { get; set; }

    public OuroMessage ToOuroMessage() => new(ChatMessage.FromAssistant(Text));

    public AddAssistantMessage(string text)
    {
        Text = text;
    }
}