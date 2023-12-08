using System;
using System.Dynamic;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.Core;
using Ouroboros.Templates.Engine;

namespace Ouroboros.Templates;

/// <summary>
/// Foundational template for all other template base. Not intended to be inherited directly, except by other template bases.
/// </summary>
public abstract class TemplateBase
{
    /// <summary>
    /// If you want to go beyond storing fields on your templates, and maintain a shared set of global fields, you can pass this
    /// in via constructor.
    /// </summary>
    internal dynamic GlobalContext { get; set; }

    /// <summary>
    /// Filename of the template we're working with. If this isn't overridden, it will attempt to derive the
    /// file from the name of the class.
    /// </summary>
    protected virtual string FileName => GetType().Name;


    #region "Public API"
    /// <summary>
    /// Renders the template as a string.
    /// </summary>
    /// <returns></returns>
    public async Task<string> RenderAsync()
    {
        return await LoadAndRender();
    }

    /// <summary>
    /// Generates as a message, inferring the role from the template name. Override to hard-code the role.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws if role cannot be determined.</exception>
    public virtual async Task<ChatMessage> AsMessage()
    {
        var role = GetMessageRole();

        return role switch
        {
            MessageRoles.System => await AsSystem(),
            MessageRoles.User => await AsUser(),
            MessageRoles.Assistant => await AsAssistant(),
            _ => throw new InvalidOperationException("Unexpected role: " + role)
        };
    }

    protected virtual MessageRoles GetMessageRole()
    {
        var fileName = FileName.ToLower();

        if (fileName.Contains("system", StringComparison.InvariantCultureIgnoreCase))
            return MessageRoles.System;

        if (fileName.Contains("user", StringComparison.InvariantCultureIgnoreCase))
            return MessageRoles.User;

        if (fileName.Contains("assistant", StringComparison.InvariantCultureIgnoreCase))
            return MessageRoles.Assistant;

        throw new InvalidOperationException($"Could not infer message role from filename: {FileName}. It must contain the words System, User, or Assistant." + 
                                            "You can also override GetMessageRole to configure the role, or call .AsSystem, .AsUser, etc.");
    }

    /// <summary>
    /// Generates as a System message.
    /// </summary>
    public virtual async Task<ChatMessage> AsSystem()
    {
        var text = await RenderAsync();

        return ChatMessage.FromSystem(text);
    }

    /// <summary>
    /// Generates as a User message.
    /// </summary>
    public virtual async Task<ChatMessage> AsUser()
    {
        var text = await RenderAsync();

        return ChatMessage.FromUser(text);
    }

    /// <summary>
    /// Generates as an Assistant message.
    /// </summary>
    public virtual async Task<ChatMessage> AsAssistant()
    {
        var text = await RenderAsync();

        return ChatMessage.FromAssistant(text);
    }
    #endregion


    #region Rendering
    private async Task<string> LoadAndRender()
    {
        var engine = new RenderEngine(this);

        return await engine.LoadAndRender(FileName);
    }
    #endregion


    protected TemplateBase()
    {
        GlobalContext = new ExpandoObject();
    }

    protected TemplateBase(object globalContext)
    {
        GlobalContext = globalContext;
    }
}
