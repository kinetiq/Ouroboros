using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Document.Elements;
using Ouroboros.Scales;
using Z.Core.Extensions;

namespace Ouroboros.Document.Extensions;

internal static class GetLastGeneratedExtensions
{
    public static ElementBase GetById(this Document @this, string id)
    {
        return (TextElement)@this
            .DocElements
            .Last(x => x.Id.EqualsIgnoreCase(id));
    }

    public static TextElement GetLastGeneratedAsElement(this Document @this)
    {
        return (TextElement) @this
            .DocElements
            .Last(x => x is TextElement { IsGenerated: true });
    }

    public static string GetLastAsText(this Document @this)
    {
        return @this
            .GetLastGeneratedAsElement()
            .Content;
    }

    public static LikertAgreement4 GetLastAsLikert(this Document @this)
    {
        return @this
            .GetLastGeneratedAsElement()
            .ToAgreementScale();
    }

}