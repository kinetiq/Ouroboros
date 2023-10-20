using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Ouroboros.Templates;

/// <summary>
/// Foundational template for all other template base. Not intended to be inherited directly, except by other template bases.
/// </summary>
public abstract class RootTemplateBase
{
    protected dynamic GlobalContext { get; set; }

    /// <summary>
    /// Filename of the template we're working with. If this isn't overridden, it will attempt to derive the
    /// file from the name of the class.
    /// </summary>
    protected virtual string FileName => GetType().Name;

    protected string GetMessageRole()
    {
        var fileName = FileName.ToLower();

        if (fileName.StartsWith("system"))
            return "system";

        if (fileName.StartsWith("user"))
            return "user";

        if (fileName.StartsWith("assistant"))
            return "assistant";

        throw new InvalidOperationException($"Could not infer message role from filename: { FileName }. It must start with System, User, or Assistant.");
    }

    protected async Task<string> LoadAndRender(object? context = null)
    {
        var nonNullableContext = context ?? new { };

        return await LoadAndRender(FileName, nonNullableContext);
    }

    protected async Task<string> LoadAndRender(string name, object localContext)
    {
        var raw = await LoadEmbeddedAsync(name);
        var final = await Render(raw, localContext);

        return final;
    }

    /// <summary>
    /// Run the raw prompt through our rendering engine.
    /// </summary>
    protected async Task<string> Render(string raw, object localContext)
    {
        var combinedContext = TypeMerger.TypeMerger.Merge(GlobalContext, localContext);

        var template = Template.Parse(raw);

        // The default is to use this_type_of_syntax, but we want to mirror our objects.
        // https://github.com/scriban/scriban/blob/master/doc/runtime.md#member-renamer
        var renamer = new MemberRenamerDelegate(member => member.Name);

        return await template.RenderAsync(combinedContext, renamer);
    }

    protected async Task<string> LoadEmbeddedAsync(string name)
    {
        var assembly = GetType().Assembly;
        var resourceName = $"{GetType().Namespace}.{name}.md";

        await using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new Exception($"Resource { resourceName } not found.\r\nMake sure the file is marked Embedded Resource and exists in the right namespace.");

        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
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
