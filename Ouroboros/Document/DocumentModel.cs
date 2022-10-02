using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ouroboros.Document.Elements;
using Z.Core.Extensions;

namespace Ouroboros.Document;

[Serializable]
internal class DocumentModel
{
    public List<ElementBase> Elements { get; set; } 
    
    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var element in Elements)
            builder.Append(element);

        return builder.ToString();
    }

    public DocumentModel()
    {
        Elements = new List<ElementBase>();
    }
}