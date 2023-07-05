using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds an assistant message to the chat context.
/// </summary>
internal class AddAssistantMessage : IChatCommand
{
    public string Text { get; set; }
    public string ElementName { get; set; }

    public OuroMessage ToOuroMessage() => new(ChatMessage.FromAssistant(Text), ElementName);

    public AddAssistantMessage(string text, string elementName = "")
    {
        Text = text;
        ElementName = elementName;
    }
}

internal class AskAndAppend : IChatCommand
{
    public string ElementName { get; set; }

    public AskAndAppend(string elementName = "")
    {
        ElementName = elementName;
    }
}

internal class RemoveLast : IChatCommand
{

}

internal class RemoveStartingAtIndex : IChatCommand
{
    public int Index { get; set; }

    public RemoveStartingAtIndex(int index)
    {
        Index = index;
    }
}

internal class RemoveStartingAtElement : IChatCommand
{
    public string ElementName { get; set; }

    public RemoveStartingAtElement(string elementName)
    {
        ElementName = elementName;
    }
}