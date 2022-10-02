using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Document.Elements
{
    [Serializable]
    [DebuggerDisplay("Text: { Content }")]
    internal class TextElement : ElementBase
    {
    }
}
