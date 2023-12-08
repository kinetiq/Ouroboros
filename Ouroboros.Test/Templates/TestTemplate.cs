using Ouroboros.Fragments;
using Ouroboros.Templates;

namespace Ouroboros.Test.Templates;

internal class TestTemplate : TemplateBase
{
    public string Test { get; set; } = "substitution";
    public Fragment TestFragment { get; set; } = new Fragment("TestFragment");
}
