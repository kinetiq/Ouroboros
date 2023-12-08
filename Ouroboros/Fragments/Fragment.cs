using System.Reflection;
using System.Threading.Tasks;

namespace Ouroboros.Fragments;

/// <summary>
/// A fragment suitable for use in a template.
/// </summary>
public class Fragment 
{
    /// <summary>
    /// Assembly we will search for the fragment.
    /// </summary>
    private Assembly Assembly { get; set; }

    /// <summary>
    /// Name of the fragment.
    /// </summary>
    private string Name { get; set; }

    /// <summary>
    /// The fragment's text after being rendered.
    /// </summary>
    private string Payload { get; set; }

    /// <summary>
    /// Options to be passed into the fragment as substitutions.
    /// </summary>
    public object? Options { get; set; }

    /// <summary>
    /// Loads the fragment from the assembly. The text goes into Payload, which is
    /// used in ToString to make this render seamlessly. This is called automatically by TemplateBase
    /// just prior to rendering.
    /// </summary>
    internal async Task RenderAsync()
    {
        Payload = await FragmentLoader.Load(Assembly, Name, Options);
    }

    public override string ToString()
    {
        return Payload;
    }

    public Fragment(Assembly assembly, string name, object? options = null)
    {
        Assembly = assembly;
        Name = name;
        Payload = "";
        Options = options;
    }

    public Fragment(string name, object? options = null)
    {
        Assembly = Assembly.GetCallingAssembly();
        Name = name;
        Payload = "";
        Options = options;
    }
}
