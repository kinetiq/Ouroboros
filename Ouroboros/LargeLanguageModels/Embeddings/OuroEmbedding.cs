using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ouroboros.LargeLanguageModels.Embeddings;

public class OuroEmbedding
{
    public int? Index { get; set; }
    public string Original { get; set; }
    public double[] Embedding { get; set; }

    public override string ToString()
    {
        return $"{Index} Text: {Original} - Dimensions: {Embedding.Length}";
    }

    public OuroEmbedding()
    {
        Original = string.Empty;
        Embedding = Array.Empty<double>();
    }
}