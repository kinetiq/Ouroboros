using Ouroboros.LargeLanguageModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Contracts.Enums;

namespace Ouroboros;
public class Constants
{
    public const OuroModels DefaultCompletionModel = OuroModels.TextDavinciV3;
    public const OuroModels DefaultChatModel = OuroModels.Gpt_5_mini;


    /// <summary>
    /// Reasoning effort to use when running in default chat mode; this only takes effect
    /// when the default chat model is being used.
    /// </summary>
    public static readonly ReasoningEffort? DefaultReasoningEffort = null;
}
