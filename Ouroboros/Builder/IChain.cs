using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Builder;

public interface IChain
{
    /// <summary>
    /// Append the specified text to the document and enqueue it for completion.
    /// </summary>
    /// <param name="text">Text to append.</param>
    /// <param name="newElementName">Optional name for the element that will be generated as a result of this.</param>
    /// <param name="options">Options that will be used for this step in the chain only.</param>
    /// <returns>A builder, which can be used to continue chaining more calls. Call ResolveAsync or one of the other async methods to submit for completion.</returns>
    public ChainBuilder Chain(string text, string? newElementName, CompleteOptions? options);

    /// <summary>
    /// Append the specified text to the document and enqueue it for completion.
    /// </summary>
    /// <param name="text">Text to append.</param>
    /// <param name="newElementName">Optional name for the element that will be generated as a result of this.</param>
    /// <returns>A builder, which can be used to continue chaining more calls. Call ResolveAsync or one of the other async methods to submit for completion.</returns>
    public ChainBuilder Chain(string text, string? newElementName = null);

    /// <summary>
    /// Append the specified text to the document and enqueue it for completion.
    /// </summary>
    /// <param name="text">Text to append.</param>
    /// <param name="options">Options that will be used for this step in the chain only.</param>
    /// <returns>A builder, which can be used to continue chaining more calls. Call ResolveAsync or one of the other async methods to submit for completion.</returns>
    public ChainBuilder Chain(string text, CompleteOptions options);
}