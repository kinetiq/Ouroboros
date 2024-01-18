using Ouroboros.Enums;

namespace Ouroboros.Extensions;

public static class LikertAgreementExtensions
{
    /// <summary>
    /// Given a string containing a likert response, this returns a likert enum.
    /// </summary>
    public static LikertAgreement4 ToAgreement4(this string @this)
    {
        // TODO: this is not in alignment with other similar extension methods; they should all take OuroResponseBase and 
        // use ExtractEnum.

        var content = @this
            .Trim()
            .ToLower();

        return content switch
        {
            "strongly disagree" => LikertAgreement4.StronglyDisagree,
            "disagree" => LikertAgreement4.Disagree,
            "agree" => LikertAgreement4.Agree,
            "strongly agree" => LikertAgreement4.StronglyAgree,
            _ => LikertAgreement4.NoMatch
        };
    }
}