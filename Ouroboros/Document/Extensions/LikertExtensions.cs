using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Document.Elements;
using Ouroboros.Scales;
using Z.Core.Extensions;

namespace Ouroboros.Document.Extensions;

internal static class LikertAgreementExtensions
{
    /// <summary>
    /// Given a TextElement containing a likert response, this returns a likert enum.
    /// </summary>
    public static LikertAgreement4 ToAgreementScale(this TextElement @this)
    {
        var content = @this.Content
            .Trim()
            .ToLower();

        return content switch
        {
            "strongly disagree" => LikertAgreement4.StronglyDisagree,
            "disagree" => LikertAgreement4.Disagree,
            "agree" => LikertAgreement4.Agree,
            "strongly agree" => LikertAgreement4.StronglyAgree,
            _ => LikertAgreement4.None
        };
    }
}