using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Ouroboros.Fragments;

public static class FragmentLoader
{
    /// <summary>
    /// Loads an embedded file using the fully qualified resource name, which includes the namespace.
    /// </summary>
    /// <param name="assembly">Assembly that contains the resource.</param>
    /// <param name="resourceName">Fully qualified resource name, including the namespace and extension. Example: MyProject.Fragments.Test.md</param>
    /// <param name="options">For merge fields.</param>
    /// <returns>Contents of the file.</returns>
    /// <exception cref="Exception">Exception is thrown if the resource does not exist.</exception>
    public static async Task<string> LoadByFullResourceName(Assembly assembly, string resourceName, object? options = null)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new Exception($"Resource {resourceName} not found.\r\nMake sure the file is marked Embedded Resource and exists in the right namespace.");

        using var reader = new StreamReader(stream);

        var raw = await reader.ReadToEndAsync();

        if (options == null)
            return raw;

        var template = Template.Parse(raw);

        // The default is to use this_type_of_syntax, but we want to mirror our objects.
        // https://github.com/scriban/scriban/blob/master/doc/runtime.md#member-renamer
        var renamer = new MemberRenamerDelegate(member => member.Name);

        return await template.RenderAsync(options, renamer);
    }

    /// <summary>
    /// Loads a resource by name. The name is the file name without the extension.
    /// </summary>
    public static async Task<string> Load(Assembly assembly, string name, object? options = null)
    {
        if (name.EndsWith(".md"))
            name = name[..^3];

        var resourceName = assembly.
            GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith(name + ".md"));

        if (resourceName == null)
            throw new InvalidOperationException($"Could not find {name} anywhere in assembly {assembly.GetName().Name}.");

        return await LoadByFullResourceName(assembly, resourceName, options);
    }
}
