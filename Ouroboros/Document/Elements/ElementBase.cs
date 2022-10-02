using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Document.Elements
{

    [Serializable]
    internal class ElementBase
    {
        public string Content { get; set; } = string.Empty;

        public override string ToString()
        {
            return Content;
        }
    }
}
