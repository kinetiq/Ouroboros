using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Chaining.Commands;
using Ouroboros.Enums;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Templates;
using Ouroboros.TextProcessing;
using Ouroboros.Tracking;

namespace Ouroboros.Chaining;

/// <summary>
/// Allows chaining of prompts using text or Ouro's templates.
/// </summary>
public class Dialog
{
    private readonly OuroClient Client;
    internal readonly List<OuroMessage> InnerMessages = new();

    /// <summary>
    /// Variable storage for named outputs. Use StoreOutputAs() to store and access via Variables["name"].
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new();

    /// <summary>
    /// Thread tracker for this dialog. Auto-generated if not provided.
    /// </summary>
    public ThreadTracker Thread { get; }

    /// <summary>
    /// Session tracker for grouping related dialogs. Null if not part of a session.
    /// </summary>
    public SessionTracker? Session { get; }

    /// <summary>
    /// Name of the prompt for logging purposes.
    /// </summary>
    public string? PromptName { get; set; }

    /// <summary>
    /// Convenience property for ThreadId.
    /// </summary>
    public Guid ThreadId => Thread.ThreadId;

    /// <summary>
    /// Convenience property for SessionId.
    /// </summary>
    public Guid? SessionId => Session?.SessionId;

    /// <summary>
    /// Allows setting options. This will be used for all operations in the chain.
    /// </summary>
    private ChatOptions? DefaultOptions;

    /// <summary>
    /// Without this, there could be an extra request at the end
    /// of the command buffer if the last command was SendAndAppend.
    /// </summary>
    private bool IsAllMessagesSent = true;

    private OuroResponseBase? LastResponse;

    public int TotalPromptTokensUsed { get; internal set; }
    public int TotalCompletionTokensUsed { get; internal set; }
    public int TotalTokensUsed { get; internal set; }

    /// <summary>
    /// Internal list of chained commands. These are executed sequentially,
    /// allowing us to chain together multiple commands and their output.
    /// </summary>
    private List<IChatCommand> Commands { get; } = new();

    private int CommandIndex = 0;

    /// <summary>
    /// Returns true if there are errors. Because Dialog does not always return
    /// a response object, this gives us a way to be sure the entire chained operation
    /// succeeded.
    /// </summary>
    public bool HasErrors { get; private set; }

    /// <summary>
    /// If there was an error, this provides a way to at least see the most recent one.
    /// </summary>
    public string LastError { get; private set; } = "";

    #region Settings

    /// <summary>
    /// Configures default options. This allows you to set the model, etc.
    /// </summary>
    public void SetDefaultChatOptions(ChatOptions options)
    {
        DefaultOptions = options;
    }

    #endregion

    #region OpenAI Calls

    /// <summary>
    /// Sends all messages in the payload to the OpenAI API.
    /// If there are any errors, the HasErrors property will be set to true and
    /// the LastError property will be set to the error message.
    /// </summary>
    private async Task<OuroResponseBase> SendMessages()
    {
        var messages = InnerMessages.Select(x => x.Message)
            .ToList();

        // Ensure tracking info is passed to ChatAsync
        var options = DefaultOptions ?? new ChatOptions();
        options.PromptName ??= PromptName;
        options.SessionId ??= SessionId;
        options.ThreadId ??= ThreadId;

        var response = await Client.ChatAsync(messages, options);

        IsAllMessagesSent = true;

        // Update token usage
        TotalPromptTokensUsed += response.PromptTokens;
        TotalCompletionTokensUsed += response.CompletionTokens ?? 0;
        TotalTokensUsed += response.TotalTokenUsage;

        // Handle errors
        if (!response.Success)
        {
            HasErrors = true;
            LastError = response.ResponseText;
        }

        return response;
    }

    #endregion

    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var message in InnerMessages)
        {
            builder.AppendLine($"**{message.Role.ToTitleCase()} Message**");
            builder.AppendLine(message.Content);
            builder.AppendLine("---");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the last response. Useful in cases where we try to extract a specific type of result and there is no match, and
    /// then
    /// we want find ourselves wanting the actual message.
    /// </summary>
    public OuroResponseBase? GetLastResponse()
    {
        return LastResponse;
    }

    public Dialog(OuroClient client)
    {
        Client = client;
        Thread = Tracker.CreateThread();
    }

    public Dialog(OuroClient client, string promptName) : this(client)
    {
        PromptName = promptName;
    }

    public Dialog(OuroClient client, DialogOptions options) : this(client)
    {
        PromptName = options.PromptName;
        Session = options.Session;
        if (options.Thread != null)
            Thread = options.Thread;
    }

    #region Builder Pattern Commands

    /// <summary>
    /// Sets the system prompt. There can only be a single system prompt,
    /// so if you run this twice, it will replace the first one.
    /// </summary>
    public Dialog SystemMessage(string prompt)
    {
        Commands.Add(new SetSystemMessage(prompt));

        return this;
    }

    /// <summary>
    /// Sets the system prompt. There can only be a single system prompt,
    /// so if you run this twice, it will replace the first one.
    /// </summary>
    public Dialog SystemMessage(ChatMessage message)
    {
        if (message.Role != ChatCompletionRole.System)
            throw new InvalidOperationException("Message must be a system message.");

        if (message.Content == null)
            throw new InvalidOperationException("Message content cannot be null.");

        Commands.Add(new SetSystemMessage(message.Content));

        return this;
    }

    /// <summary>
    /// Sets the system prompt. There can only be a single system prompt,
    /// so if you run this twice, it will replace the first one.
    /// </summary>
    public Dialog SystemMessage(TemplateBase template)
    {
        Commands.Add(new SetSystemTemplateMessage(template));

        return this;
    }

    /// <summary>
    /// Adds an assistant message as the next message.
    /// </summary>
    public Dialog AssistantMessage(string prompt)
    {
        Commands.Add(new AddAssistantMessage(prompt));

        return this;
    }

    /// <summary>
    /// Adds an assistant message as the next message.
    /// </summary>
    public Dialog AssistantMessage(ChatMessage message)
    {
        if (message.Role != ChatCompletionRole.Assistant)
            throw new InvalidOperationException("Message must be an assistant message.");

        if (message.Content == null)
            throw new InvalidOperationException("Message content cannot be null.");

        Commands.Add(new AddAssistantMessage(message.Content));

        return this;
    }

    /// <summary>
    /// Adds an assistant message as the next message.
    /// </summary>
    public Dialog AssistantMessage(TemplateBase template)
    {
        Commands.Add(new AddAssistantTemplateMessage(template));

        return this;
    }

    /// <summary>
    /// Adds a user message as the next message.
    /// </summary>
    public Dialog UserMessage(string prompt)
    {
        Commands.Add(new AddUserMessage(prompt));

        return this;
    }

    /// <summary>
    /// Adds a user message as the next message.
    /// </summary>
    public Dialog UserMessage(ChatMessage message)
    {
        if (message.Role != ChatCompletionRole.User)
            throw new InvalidOperationException("Message must be a user message.");

        if (message.Content == null)
            throw new InvalidOperationException("Message content cannot be null.");

        Commands.Add(new AddUserMessage(message.Content));

        return this;
    }

    /// <summary>
    /// Adds a user message as the next message.
    /// </summary>
    public Dialog UserMessage(TemplateBase template)
    {
        Commands.Add(new AddUserTemplateMessage(template));

        return this;
    }

    /// <summary>
    /// Sends the chat payload for completion and adds the result as an Assistant message.
    /// </summary>
    public Dialog SendAndAppend()
    {
        Commands.Add(new SendAndAppend());

        return this;
    }

    /// <summary>
    /// Stores the last response in a variable. Access via Variables["name"].
    /// </summary>
    public Dialog StoreOutputAs(string variableName)
    {
        Commands.Add(new StoreOutputAs(variableName));

        return this;
    }

    /// <summary>
    /// Removes the last item in the chat payload.
    /// </summary>
    public Dialog RemoveLast()
    {
        Commands.Add(new RemoveLast());

        return this;
    }

    /// <summary>
    /// Removes the message at index and all messages after it.
    /// </summary>
    public Dialog RemoveStartingAt(int index)
    {
        Commands.Add(new RemoveStartingAtIndex(index));

        return this;
    }



    #endregion

    #region Terminators

    /// <summary>
    /// Sends the chat payload for completion and converts the result to a string.
    /// If there was an error, this will be an error message. Be sure to check the dialog
    /// for errors via Dialog.HasErrors
    /// </summary>
    public async Task<string> SendToString()
    {
        var response = await ExecuteChainableCommands();

        // This works even if there are errors. But it might be an error message.
        return response.ResponseText;
    }

    /// <summary>
    /// Sends the chat payload for completion, then senses the list type and splits the text into a list.
    /// Works with numbered lists and lists separated by any type of newline.
    /// </summary>
    public async Task<List<ListItem>> SendAndExtractList()
    {
        var response = await ExecuteChainableCommands();

        return response.ExtractList();
    }

    /// <summary>
    /// Sends the chat payload for completion, then splits the result into a numbered list.
    /// Any item that doesn't start with a number is discarded. Note that this is different from SendAndExtractList
    /// in a few ways, including the result type, which in this case is able to include the item number (since these
    /// items are numbered).
    /// </summary>
    public async Task<List<NumberedListItem>> SendAndExtractNumberedList()
    {
        var response = await ExecuteChainableCommands();

        return response.ExtractNumberedList();
    }

    /// <summary>
    /// Extracts to an arbitrary enum. The enum must have a member called "NoMatch", or there will be exceptions at
    /// runtime.
    /// </summary>
    /// <typeparam name="TEnum">An enum that has a member called NoMatch.</typeparam>
    public async Task<TEnum> ExtractEnum<TEnum>() where TEnum : struct, Enum
    {
        var response = await ExecuteChainableCommands();

        var enumResult = response.ExtractEnum<TEnum>();

        // If we didn't get a match, and we don't already have an error, set the error. 
        // If we do have an error, that will be the more specific problem, so we don't want to overwrite it.
        if (enumResult.IsNoMatch() && HasErrors == false)
        {
            HasErrors = true;
            LastError = $"After executing the chain with no errors, tried to extract to {typeof(TEnum).Name} from the " +
                        "response text, but no match was found. Check LastResponse for more details or use ExecuteToString instead.";
        }

        return enumResult;
    }

    public async Task<string> ExecuteToString()
    {
        var result = await ExecuteChainableCommands();
        return result.ResponseText;
    }

    /// <summary>
    /// Returns an enum containing Yes, No, or NoMatch if we are unable to parse the response.
    /// NoMatch results in the dialog being marked as having errors.
    /// </summary>
    public async Task<YesNo> ExecuteToYesNo()
    {
        var response = await ExecuteChainableCommands();

        var result = response.ExtractYesNo();

        if (result == YesNo.NoMatch && HasErrors == false)
        {
            HasErrors = true;
            LastError = $"After executing the chain with no errors, tried to extract to {nameof(YesNo)} from the response text, " +
                        "but no match was found. Check LastResponse for more details or use ExecuteToString instead.";
        }

        return result;
    }

    /// <summary>
    /// Extracts the results to an arbitrary model. If the model cannot be fully bound, an error is set on the dialog.
    /// </summary>
    public async Task<T> ExtractModel<T>() where T : CodexModel
    {
        var response = await Execute();

        var codex = new HermeticCodex<T>();

        var result = codex.Bind(response.ResponseText);

        // If there is a problem binding the model, set the error. But if there was an error deeper in the chain, don't overwrite it.
        if (!result.IsComplete() && HasErrors == false)
        {
            HasErrors = true;
            LastError = "HermeticCodex > Generation appears to be successful, however the model could not be fully bound. " +
                        "Check the model for more details.";
        }

        return result;
    }

    /// <summary>
    /// Sends the chat payload for completion and converts the result to a ResponseBase.
    /// </summary>
    public async Task<OuroResponseBase> Send()
    {
        return await ExecuteChainableCommands();
    }

    /// <summary>
    /// Use this when you want to execute the chain and there isn't a reasonable terminator.
    /// </summary>
    public async Task<OuroResponseBase> Execute()
    {
        return await ExecuteChainableCommands();
    }

    #endregion

    #region Command Execution and Handling

    /// <summary>
    /// Executes all commands that were chained in, and returns the last response.
    /// If there are any errors, we immediately stop and return.
    /// </summary>
    private async Task<OuroResponseBase> ExecuteChainableCommands()
    {
        LastResponse = null;

        for (; CommandIndex < Commands.Count; CommandIndex++)
        {
            var command = Commands[CommandIndex];
            switch (command)
            {
                case SendAndAppend sendAndAppend:
                    var response = await HandleSendAndAppend(sendAndAppend);

                    // If there is an error talking to openAI, stop execution immediately.
                    if (!response.Success)
                        return response;

                    break;
                case SetSystemMessage message:
                    HandleSetSystemMessage(message);
                    break;
                case SetSystemTemplateMessage template:
                    await HandleSetSystemTemplateMessage(template);
                    break;
                case AddAssistantMessage message:
                    HandleAssistantMessage(message);
                    break;
                case AddAssistantTemplateMessage template:
                    await HandleAssistantTemplateMessage(template);
                    break;
                case AddUserMessage message:
                    HandleUserMessage(message);
                    break;
                case AddUserTemplateMessage template:
                    await HandleUserTemplateMessage(template);
                    break;
                case RemoveLast removeLast:
                    HandleRemoveLast(removeLast);
                    break;
                case RemoveStartingAtIndex removeStartingAtIndex:
                    HandleRemoveStartingAtIndex(removeStartingAtIndex);
                    break;
                case StoreOutputAs storeOutputAs:
                    HandleStoreOutputAs(storeOutputAs);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled command: {nameof(command)}");
            }
        }

        // If we've already sent all messages, just return the last response.
        if (IsAllMessagesSent)
        {
            if (LastResponse == null)
                return new OuroResponseNoOp();

            return LastResponse;
        }

        // Otherwise, execute.
        return await SendMessages();
    }

    private void HandleStoreOutputAs(StoreOutputAs storeOutputAs)
    {
        if (LastResponse == null)
            throw new InvalidOperationException("A response is required. Use SendAndAppend() before calling StoreOutputAs().");

        Variables[storeOutputAs.VariableName] = LastResponse.ResponseText;
    }

    private void HandleRemoveStartingAtIndex(RemoveStartingAtIndex removeStartingAtIndex)
    {
        if (!InnerMessages.Any())
            return;

        InnerMessages.RemoveRange(removeStartingAtIndex.Index, InnerMessages.Count - removeStartingAtIndex.Index);
    }

    private void HandleRemoveLast(RemoveLast removeLast)
    {
        if (!InnerMessages.Any())
            return;

        InnerMessages.RemoveAt(InnerMessages.Count - 1);
    }

    private void HandleUserMessage(AddUserMessage userMessage)
    {
        IsAllMessagesSent = false;

        InnerMessages.Add(userMessage.ToOuroMessage());
    }

    private async Task HandleUserTemplateMessage(AddUserTemplateMessage template)
    {
        IsAllMessagesSent = false;

        InnerMessages.Add(await template.ToOuroMessage());
    }

    private void HandleAssistantMessage(AddAssistantMessage assistantMessage)
    {
        IsAllMessagesSent = false;

        InnerMessages.Add(assistantMessage.ToOuroMessage());
    }

    private async Task HandleAssistantTemplateMessage(AddAssistantTemplateMessage template)
    {
        IsAllMessagesSent = false;

        InnerMessages.Add(await template.ToOuroMessage());
    }

    private void HandleSetSystemMessage(SetSystemMessage systemMessage)
    {
        IsAllMessagesSent = false;

        // Remove any existing system message and drop this at position 0.
        if (InnerMessages.Any() && InnerMessages[0].Role == ChatCompletionRole.System)
            InnerMessages.RemoveAt(0);

        InnerMessages.Insert(0, systemMessage.ToOuroMessage());
    }

    private async Task HandleSetSystemTemplateMessage(SetSystemTemplateMessage template)
    {
        IsAllMessagesSent = false;

        // Remove any existing system message and drop this at position 0.
        if (InnerMessages.Any() && InnerMessages[0].Role == ChatCompletionRole.System)
            InnerMessages.RemoveAt(0);

        InnerMessages.Insert(0, await template.ToOuroMessage());
    }

    private async Task<OuroResponseBase> HandleSendAndAppend(SendAndAppend sendAndAppend)
    {
        var response = await SendMessages();
        LastResponse = response;

        if (response.Success)
        {
            var message = ChatMessage.FromAssistant(response.ResponseText);
            InnerMessages.Add(new OuroMessage(message));
        }

        return response;
    }

    #endregion
}