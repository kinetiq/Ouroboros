using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.Templates.Engine;

namespace Ouroboros.Templates;

/// <summary>
/// Foundational template for all other template base. Not intended to be inherited directly, except by other template bases.
/// </summary>
public abstract class RootTemplateBase
{
    internal dynamic GlobalContext { get; set; }

    /// <summary>
    /// Filename of the template we're working with. If this isn't overridden, it will attempt to derive the
    /// file from the name of the class.
    /// </summary>
    protected virtual string FileName => GetType().Name;

    protected virtual MessageRoles GetMessageRole()
    {
        var fileName = FileName.ToLower();

        if (fileName.StartsWith("system"))
            return MessageRoles.System;

        if (fileName.StartsWith("user"))
            return MessageRoles.User;

        if (fileName.StartsWith("assistant"))
            return MessageRoles.Assistant;

        throw new InvalidOperationException($"Could not infer message role from filename: { FileName }. It must start with System, User, or Assistant.");
    }

    protected async Task<string> LoadAndRender(string name)
    {
        var engine = new RenderEngine(this);

        return await engine.LoadAndRender(name);
    }

    protected async Task<string> LoadAndRender()
    {
        return await LoadAndRender(FileName);
    }

    protected RootTemplateBase()
    {
        GlobalContext = new ExpandoObject();
    }

    protected RootTemplateBase(object globalContext)
    {
        GlobalContext = globalContext;
    }
}
