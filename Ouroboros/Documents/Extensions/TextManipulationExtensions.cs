using Ouroboros.Documents.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Documents.Extensions
{
    public static class TextManipulationExtensions
    {
        /// <summary>
        /// Uses a flexible approach that should work with various platforms.
        /// </summary>
        public static string[] SplitTextOnNewLines(this TextElement @this, StringSplitOptions options = StringSplitOptions.None)
        {
            return @this
                .Text
                .Split(
                    new string[] { "\r\n", "\r", "\n" },  // flexible approach
                    options);
        }
    }
}
