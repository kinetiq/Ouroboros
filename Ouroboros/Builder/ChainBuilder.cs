using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using Ouroboros.LargeLanguageModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ouroboros.Builder;

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
    /// Assumes the last prompt returned was a text-based list of some kind, with items separated by newlines.
    /// Cleans up any extra whitespace and returns list().
    /// </summary>
    public async Task<List<string>> AsListAsync()
    {
        var response = await ResolveAsync();

        // TODO: better response handling
        if (response is OuroResponseFailure)
            throw new InvalidOperationException($"Error calling LLM: {response}");

        var last = Document.GetLastGeneratedAsElement();

        var options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        // turn the text from the last element into a list by splitting on newlines
        var lineList = last
            .SplitTextOnNewLines(options)
            .ToList();

        return lineList.ToList();
    }

    public async Task<Document> AsDocumentAsync()
    {
        var response = await ResolveAsync();

        // TODO: better response handling
        if (response is OuroResponseFailure)
            throw new InvalidOperationException($"Error calling LLM: {response}");

        return Document;
    }

    public async Task<string> AsString()
    {
        var response = await ResolveAsync();
        
        // TODO: better response handling
        if (response is OuroResponseFailure)
            throw new InvalidOperationException($"Error calling LLM: {response}");

        return response.ResponseText;
    }
    #endregion


    /// <summary>
    /// Resolves all queued commands.
    /// </summary>
    public async Task<OuroResponseBase> ResolveAsync()
    {
        OuroResponseBase lastResponse = new OuroResponseNoOp();
        
        foreach (var command in Commands)
        {
            if (command.Text is { Length: > 0 })
                Document.AddText(command.Text);

            lastResponse = await Document.ResolveAndSubmit(command.NewElementName);

            if (lastResponse is OuroResponseFailure)
                return lastResponse;
        }

        Commands.Clear();

        return lastResponse;
    } 

    /// <summary>
    /// Adds a command to the queue, to be executed when one of the async calls occurs.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="newElementName"></param>
    /// <param name="options"></param>
    public void AddCommand(string? text, string? newElementName = null, CompleteOptions? options = null)
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