using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Events
{
    public class OnRequestCompletedArgs
    {
        public string Prompt { get; set; } 
        public OuroResponseBase Response { get; set; } 
        public int Tokens { get; set; }

        public OnRequestCompletedArgs()
        {
            Prompt = "";
            Response = new OuroResponseNoOp();
            Tokens = 0;
        }
    }
}
