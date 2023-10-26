using System;
using System.Dynamic;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.Core;

namespace Ouroboros.Templates;

public abstract class TemplateBase : RootTemplateBase
{
    public async Task<string> Generate()
    {
        return await LoadAndRender();
    }

    /// <summary>
    /// Generates as a message, inferring the role from the beginning of the template name.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws if the start of the template is not a valid message role.</exception>
    public virtual async Task<ChatMessage> AsMessage()
    {
        var role = GetMessageRole();

        return role switch
        {
            MessageRoles.System    => await AsSystem(),
            MessageRoles.User      => await AsUser(),
            MessageRoles.Assistant => await AsAssistant(),
            _           => throw new InvalidOperationException("Unexpected role: " + role)
        };
    }

    /// <summary>
    /// Generates as a System message.
    /// </summary>
    public virtual async Task<ChatMessage> AsSystem()
    {
        var text = await Generate();

        return ChatMessage.FromSystem(text);
    }

    /// <summary>
    /// Generates as a User message.
    /// </summary>
    public virtual async Task<ChatMessage> AsUser()
    {
        var text = await Generate();

        return ChatMessage.FromUser(text);
    }

    /// <summary>
    /// Generates as an Assistant message.
    /// </summary>
    public virtual async Task<ChatMessage> AsAssistant()
    {
        var text = await Generate();

        return ChatMessage.FromAssistant(text);
    }

    protected TemplateBase()
    {
        GlobalContext = new ExpandoObject();
    }

    protected TemplateBase(object globalContext) : base(globalContext)
    {
    }
}