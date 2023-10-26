using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Test.Templates;
public class TemplateTests
{
    [Fact]
    public async Task Simple_Substitutions_Work()
    {
        var template = new TestTemplate();

        var result = await template.RenderAsync();

        Assert.Equal("This is a test substitution.\r\n\r\nThis is a test fragment. Yeehaw.", result);
    }

}
