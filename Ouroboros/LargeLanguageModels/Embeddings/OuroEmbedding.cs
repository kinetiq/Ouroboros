using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ouroboros.LargeLanguageModels.Embeddings;

public class OuroEmbedding
{
    public int? Index { get; set; }
    public string Original { get; set; }
    public List<double> Embedding { get; set; }

    public OuroEmbedding()
    {
        Original = string.Empty;
        Embedding = new List<double>();
    }
}