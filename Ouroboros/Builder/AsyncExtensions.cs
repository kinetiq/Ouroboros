using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Builder
{
    public static class AsyncExtensions
    {
        /// <summary>
        /// Extension methods that make chaining async methods easier.
        /// </summary>
        public static async Task<AskBuilder> Ask(this Task<AskBuilder> @this, string text, string newElementName = "")
        {
            return await @this.Result.Ask(text, newElementName);
        }
    }
}
