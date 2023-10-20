using System;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.Templates;

public abstract class SimpleTemplateBase : RootTemplateBase
{

    /// <summary>
    /// Generates text from the template. Context can be any object, including an anonymous object. If there
    /// are no parameters, just leave it out or pass in null.
    /// </summary>
    public virtual async Task<string> Generate(object? context = null)
    {
        return await LoadAndRender(context);
    }

    /// <summary>
    /// Generates as a message, inferring the role from the beginning of the template name.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws if the start of the template is not a valid message role.</exception>
    public virtual async Task<ChatMessage> AsMessage(object? context = null)
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
    public virtual async Task<ChatMessage> AsSystem(object? context = null)
    {
        var text = await Generate(context);

        return ChatMessage.FromSystem(text);
    }


    /// <summary>
    /// Generates as a User message.
    /// </summary>

    public virtual async Task<ChatMessage> AsUser(object? context = null)
    {
        var text = await Generate(context);

        return ChatMessage.FromUser(text);
    }

    /// <summary>
    /// Generates as an Assistant message.
    /// </summary>
    public virtual async Task<ChatMessage> AsAssistant(object? context = null)
    {
        var text = await Generate(context);

        return ChatMessage.FromAssistant(text);
    }

    protected SimpleTemplateBase() : base()
    {
    }

    protected SimpleTemplateBase(object context) : base(context)
    {
    }
}