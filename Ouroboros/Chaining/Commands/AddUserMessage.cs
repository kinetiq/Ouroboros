using Ouroboros.Core;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds a user message to the chat context.
/// </summary>
internal class AddUserMessage : IChatCommand
{
    public string Text { get; set; }

    public OuroMessage ToOuroMessage() => OuroMessage.FromUser(Text);

    public AddUserMessage(string text)
    {
        Text = text;
    }
}