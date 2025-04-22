using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Templates;
using System.Threading.Tasks;

namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Adds an assistant message to the chat context.
/// </summary>
internal class AddAssistantTemplateMessage : IChatCommand
{
    public TemplateBase Template { get; set; }
    public string ElementName { get; set; }

    public async Task<OuroMessage> ToOuroMessage()
    {
        var message = await Template.AsAssistant();

        return new OuroMessage(message, ElementName);
    }

    public AddAssistantTemplateMessage(TemplateBase template, string elementName = "")
    {
        Template = template;
        ElementName = elementName;
    }
}