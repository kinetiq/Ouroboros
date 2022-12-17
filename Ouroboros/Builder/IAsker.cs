using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Builder
{
    public interface IAsker
    {
        public Task<AskBuilder> Ask(string text, string newElementName = "");
    }
}
