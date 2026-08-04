using System.Threading.Tasks;
using Ouroboros.Templates;
using Ouroboros.Core;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds a user message to the chat context.
/// </summary>
internal class AddUserTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        return await Template.AsUser();
    }

    public AddUserTemplateMessage(TemplateBase template)
    {
        Template = template;
    }
}