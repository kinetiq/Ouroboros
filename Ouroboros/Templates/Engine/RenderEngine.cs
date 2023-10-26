using Ouroboros.Fragments;
using Scriban.Runtime;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Ouroboros.Templates.Engine;

/// <summary>
/// Handles template rendering.
/// </summary>
internal class RenderEngine
{
    private readonly TemplateBase Template;

    internal RenderEngine(TemplateBase template)
    {
        Template = template;
    }

    internal async Task<string> LoadAndRender(string name)
    {
        var raw = await LoadEmbeddedAsync(name);
        var final = await Render(raw);

        return final;
    }

    protected async Task<string> LoadEmbeddedAsync(string name)
    {
        var assembly = Template.GetType().Assembly;
        var resourceName = $"{Template.GetType().Namespace}.{name}.md";

        await using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new Exception($"Resource {resourceName} not found.\r\nMake sure the file is marked Embedded Resource and exists in the right namespace.");

        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }


    /// <summary>
    /// Run the raw prompt through our rendering engine.
    /// </summary>
    protected async Task<string> Render(string raw)
    {
        await RenderFragmentsAsync(Template);

        var combinedContext = TypeMerger.TypeMerger.Merge(Template.GlobalContext, Template);

        var template = Scriban.Template.Parse(raw);

        // The default is to use this_type_of_syntax, but we want to mirror our objects.
        // https://github.com/scriban/scriban/blob/master/doc/runtime.md#member-renamer
        var renamer = new MemberRenamerDelegate(member => member.Name);

        return await template.RenderAsync(combinedContext, renamer);
    }

    /// <summary>
    /// Calls RenderAsync on all fragments, which gets them ready to merge.
    /// </summary>
    public async Task RenderFragmentsAsync(object obj)
    {
        var objectType = obj.GetType();
        var properties = objectType.GetProperties();

        foreach (var property in properties)
        {
            if (property.PropertyType != typeof(Fragment))
                continue;

            var fragment = (Fragment)property.GetValue(obj);

            if (fragment != null)
            {
                await fragment.RenderAsync();
            }
        }
    }
}
