using System.Collections.Generic;

namespace Ouroboros.LargeLanguageModels.Embeddings;

public abstract class EmbeddingResponseBase
{
    public bool Success { get; set; }
    public List<OuroEmbedding> Embeddings { get; set; } 

    public string ResponseText { get; set; } 

    public override string ToString()
    {
        return ResponseText;
    }

    protected EmbeddingResponseBase()
    {
        Embeddings = new List<OuroEmbedding>();
        ResponseText = string.Empty;
    }
}

public class EmbeddingResponseSuccess : EmbeddingResponseBase
{
    public EmbeddingResponseSuccess(List<OuroEmbedding> embeddings)
    {
        Success = true;
        Embeddings = embeddings;
        ResponseText = string.Empty;
    }
}


/// <summary>
/// Indicates that the API call was cancelled because there was nothing to do.
/// </summary>
public class EmbeddingResponseNoOp : EmbeddingResponseBase
{
    public EmbeddingResponseNoOp()
    {
        Success = true;
        Embeddings = new List<OuroEmbedding>();
        ResponseText = string.Empty;
    }
}


public class EmbeddingResponseFailure : EmbeddingResponseBase
{
    public EmbeddingResponseFailure(string error)
    {
        Success = false;
        Embeddings = new List<OuroEmbedding>();
        ResponseText = error;
    }
}