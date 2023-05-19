using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.TextProcessing;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ouroboros.Builder;

/// <summary>
/// Enables chaining multiple prompts together. If you have no idea what this is and just want to
/// complete a basic prompt, try calling .CompleteToString()
/// </summary>
public class ChainBuilder : IChain
{
    internal Document Document { get; set; }
    private List<ChainedCommand> Commands { get; set; }
    private CompleteOptions? DefaultOptions = null;

    #region Public API
  
    /// <inheritdoc/>
    public ChainBuilder Chain(string text, string? newElementName, CompleteOptions? options)
    {
        AddCommand(text, newElementName, options ?? DefaultOptions);

        return this;
    }

    /// <inheritdoc/>
    public ChainBuilder Chain(string text, string? newElementName = null)
    {
        return Chain(text, newElementName, DefaultOptions);
    }

    /// <inheritdoc/>
    public ChainBuilder Chain(string text, CompleteOptions options)
    {
        AddCommand(text, null, options);

        return this;
    }

    /// <summary>
    /// Set options to be used for all completions initiated from this builder, not just subsequent ones.
    /// This can be overriden on individual chains by passing in options there.  
    /// </summary>
    public ChainBuilder SetDefaultOptions(CompleteOptions options)
    {
        DefaultOptions = options;
        
        return this;
    } 

    /// <summary>
    /// Assumes the last completion returned was a list, with items separated by newlines or numbers.
    /// Cleans up any extra whitespace and returns a list.
    /// </summary>
    public async Task<PromptResponse<List<ListItem>>> CompleteToListAsync()
    {
        var response = await ExecuteCommandsAsync();

        if (!response.Success)
            return new PromptResponse<List<ListItem>>(response);

        var value = ListExtractor.Extract(response.ResponseText);

        return new PromptResponse<List<ListItem>>(response, value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<PromptResponse<Document>> CompleteToDocumentAsync()
    {
        var response = await ExecuteCommandsAsync();

        if (!response.Success)
            return new PromptResponse<Document>(response);

        return new PromptResponse<Document>(response, Document);
    }
    
    public async Task<PromptResponse<string>> CompleteToStringAsync()
    {
        var response = await ExecuteCommandsAsync();

        if (!response.Success)
            return new PromptResponse<string>(response);

        return new PromptResponse<string>(response, response.ResponseText);
    }
    #endregion


    /// <summary>
    /// Executes all queued commands, which means actually sending requests to the LLM. You don't usually call this
    /// manually. Instead, call one of the CompleteTo methods.
    /// </summary>
    internal async Task<CompleteResponseBase> ExecuteCommandsAsync()
    {
        CompleteResponseBase lastResponse = new CompleteResponseNoOp();
        
        foreach (var command in Commands)
        {
            if (command.Text is { Length: > 0 })
                Document.AddText(command.Text);

            lastResponse = await Document.ResolveAndSubmitAsync(command.NewElementName);

            if (lastResponse is CompleteResponseFailure)
                return lastResponse;
        }

        Commands.Clear();

        return lastResponse;
    } 

    /// <summary>
    /// Adds a command to the queue, to be executed when one of the async calls occurs.
    /// </summary>
    /// <param name="text">The text to be added to the existing document. If this is null, the document will be completed without adding anything.</param>
    /// <param name="newElementName">The name for the new element. Will be ignored if text is null.</param>
    /// <param name="options">Optional options that impact this completion only. If it's null, the defaults for this ChainBuilder will be used, if they exist.</param>
    internal void AddCommand(string? text, string? newElementName = null, CompleteOptions? options = null)
    {
        var command = new ChainedCommand()
        {
            Text = text,
            NewElementName = newElementName,
            Options = options
        };

        Commands.Add(command);
    }
    
    public ChainBuilder(Document document, string text, string? newElementName = null, CompleteOptions? options = null)
    {
        Commands = new List<ChainedCommand>();
        Document = document;

        AddCommand(text, newElementName, options);
    }

    public ChainBuilder(Document document, CompleteOptions? options = null)
    {
        Commands = new List<ChainedCommand>();
        Document = document;

        AddCommand(null, null, options); // strange looking command, it means just resolve the document.
    }
}