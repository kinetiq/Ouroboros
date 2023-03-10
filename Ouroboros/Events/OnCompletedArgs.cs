using Ouroboros.LargeLanguageModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Events
{
    public class OnRequestCompletedArgs
    {
        public string Prompt { get; set; }
        public OuroResponseBase Response { get; set; } 
        public int Tokens { get; set; }
    }
}
