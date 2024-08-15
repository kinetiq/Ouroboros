using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.Templates;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds a user message to the chat context.
/// </summary>
internal class AddUserTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }
    public string ElementName { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        var message = await Template.AsUser();

        return new OuroMessage(message, ElementName);
    }

    public AddUserTemplateMessage(TemplateBase template, string elementName = "")
    {
        Template = template;
        ElementName = elementName;
    }
}