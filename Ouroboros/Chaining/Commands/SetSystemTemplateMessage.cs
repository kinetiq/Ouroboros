using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Templates;
using Ouroboros.Core;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Sets the system message at index 0, removing any existing system message.
/// </summary>
internal class SetSystemTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        return await Template.AsSystem();
    }

    public SetSystemTemplateMessage(TemplateBase template)
    {
        Template = template;
    }
}