using Ouroboros.Chaining;
using System.Linq;
using Ouroboros.Enums;
using Z.Core.Extensions;

namespace Ouroboros.Extensions;

public static class DialogExtensions
{
    public static string GetByName(this Dialog @this, string elementName)
    {
        return @this
            .InnerMessages
            .Last(x => x.ElementName.EqualsIgnoreCase(elementName))
            .Content;
    }

    public static string GetLast(this Dialog @this)
    {
        return @this
            .InnerMessages
            .Last()
            .Content;
    }

    public static string GetLast(this Dialog @this, string role)
    {
        return @this
            .InnerMessages
            .Last(x => x.Role == role)
            .Content;
    }

    public static LikertAgreement4 GetLastAsLikert(this Dialog @this)
    {
        return @this.GetLast()
            .ToAgreement4();
    }
}