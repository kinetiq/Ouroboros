using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Templates;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Sets the system message at index 0, removing any existing system message.
/// </summary>
internal class SetSystemTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        var message = await Template.AsSystem();

        return new OuroMessage(message);
    }

    public SetSystemTemplateMessage(TemplateBase template)
    {
        Template = template;
    }
}