using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Chaining;

public class OuroMessage 
{
    internal readonly ChatMessage Message;
    internal string ElementName { get; set; }

    internal string Role => Message.Role;
    internal string Content => Message.Content;

    internal OuroMessage(ChatMessage message, string elementName = "")
    {
        Message = message;
        ElementName = elementName;
    }
}
