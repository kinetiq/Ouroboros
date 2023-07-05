using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Sets the system message at index 0, removing any existing system message.
/// </summary>
internal class SetSystemMessage : IChatCommand
{
    public string Text { get; set; }

    public OuroMessage ToOuroMessage() => new(ChatMessage.FromSystem(Text));

    public SetSystemMessage(string text)
    {
        Text = text;
    }
}