using Ouroboros.Templates;
using System.Threading.Tasks;
using Ouroboros.Core;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds an assistant message to the chat context.
/// </summary>
internal class AddAssistantTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        return await Template.AsAssistant();
    }

    public AddAssistantTemplateMessage(TemplateBase template)
    {
        Template = template;
    }
}