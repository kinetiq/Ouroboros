using System;
using System.Dynamic;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Templates;

public abstract class OptionsTemplateBase<T> : RootTemplateBase
{
    public async Task<string> Generate(T options)
    {
        return await LoadAndRender(options);
    }

    /// <summary>
    /// Generates as a message, inferring the role from the beginning of the template name.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws if the start of the template is not a valid message role.</exception>
    public virtual async Task<ChatMessage> AsMessage(T context)
    {
        var role = GetMessageRole();

        return role switch
        {
            "system"    => await AsSystem(context),
            "user"      => await AsUser(context),
            "assistant" => await AsAssistant(context),
            _           => throw new InvalidOperationException("Unexpected role: " + role)
        };
    }

    /// <summary>
    /// Generates as a System message.
    /// </summary>
    public virtual async Task<ChatMessage> AsSystem(T context)
    {
        var text = await Generate(context);

        return ChatMessage.FromSystem(text);
    }

    /// <summary>
    /// Generates as a User message.
    /// </summary>
    public virtual async Task<ChatMessage> AsUser(T context)
    {
        var text = await Generate(context);

        return ChatMessage.FromUser(text);
    }

    /// <summary>
    /// Generates as an Assistant message.
    /// </summary>
    public virtual async Task<ChatMessage> AsAssistant(T context)
    {
        var text = await Generate(context);

        return ChatMessage.FromAssistant(text);
    }


    protected OptionsTemplateBase()
    {
        GlobalContext = new ExpandoObject();
    }

    protected OptionsTemplateBase(object globalContext) : base(globalContext)
    {
    }
}