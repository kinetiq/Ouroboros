using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.Chaining.Commands;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.TextProcessing;

namespace Ouroboros.Chaining;

public class Dialog
{
    private readonly OuroClient Client;
    internal readonly List<OuroMessage> InnerMessages = new();

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

        var response = await Client.ChatAsync(messages, DefaultOptions);

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
            builder.AppendLine(
                $"**{message.Role.ToTitleCase()} Message**{(message.ElementName.IsNullOrWhiteSpace() ? "" : " (" + message.ElementName + ")")}");
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
    }

    #region Builder Pattern Commands

    /// <summary>
    /// Sets the system prompt. There can only be a single system prompt,
    /// so if you run this twice, it will replace the first one.
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public Dialog SystemMessage(string prompt)
    {
        Commands.Add(new SetSystemMessage(prompt));

        return this;
    }

    /// <summary>
    /// Adds an assistant message as the next message.
    /// </summary>
    public Dialog AssistantMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddAssistantMessage(prompt, elementName));

        return this;
    }

    /// <summary>
    /// Adds a user message as the next message.
    /// </summary>
    public Dialog UserMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddUserMessage(prompt, elementName));

        return this;
    }

    /// <summary>
    /// Sends the chat payload for completion and adds the result as an Assistant message.
    /// </summary>
    public Dialog SendAndAppend(string elementName = "")
    {
        Commands.Add(new SendAndAppend(elementName));

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

    /// <summary>
    /// Removes the message named elementName and all messages after it.
    /// </summary>
    public Dialog RemoveStartingAt(string elementName)
    {
        Commands.Add(new RemoveStartingAtElement(elementName));

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

        foreach (var command in Commands)
            switch (command)
            {
                case SendAndAppend sendAndAppend:
                    var response = await HandleSendAndAppend(sendAndAppend);

                    // If there is an error talking to openAI, stop execution immediately.
                    if (!response.Success)
                        return response;

                    break;
                case SetSystemMessage systemMessage:
                    HandleSetSystemMessage(systemMessage);
                    break;
                case AddAssistantMessage assistantMessage:
                    HandleAssistantMessage(assistantMessage);
                    break;
                case AddUserMessage userMessage:
                    HandleUserMessage(userMessage);
                    break;
                case RemoveLast removeLast:
                    HandleRemoveLast(removeLast);
                    break;
                case RemoveStartingAtIndex removeStartingAtIndex:
                    HandleRemoveStartingAtIndex(removeStartingAtIndex);
                    break;
                case RemoveStartingAtElement removeStartingAtElement:
                    HandleRemoveStartingAtElement(removeStartingAtElement);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled command: {nameof(command)}");
            }

        // If we've already sent all messages, just return the last response.
        if (IsAllMessagesSent)
            return LastResponse!;

        // Otherwise, execute.
        return await SendMessages();
    }

    private void HandleRemoveStartingAtElement(RemoveStartingAtElement removeStartingAtElement)
    {
        if (!InnerMessages.Any())
            return;

        var index = InnerMessages.FindIndex(x => x.ElementName == removeStartingAtElement.ElementName);

        if (index != -1)
            InnerMessages.RemoveRange(index, InnerMessages.Count - index);
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

    private void HandleAssistantMessage(AddAssistantMessage assistantMessage)
    {
        IsAllMessagesSent = false;

        InnerMessages.Add(assistantMessage.ToOuroMessage());
    }

    private void HandleSetSystemMessage(SetSystemMessage systemMessage)
    {
        IsAllMessagesSent = false;

        // Remove any existing system message and drop this at position 0.
        if (InnerMessages.Any() && InnerMessages[0].Role == StaticValues.ChatMessageRoles.System)
            InnerMessages.RemoveAt(0);

        InnerMessages.Insert(0, systemMessage.ToOuroMessage());
    }

    private async Task<OuroResponseBase> HandleSendAndAppend(SendAndAppend sendAndAppend)
    {
        var response = await SendMessages();
        LastResponse = response;

        if (response.Success)
        {
            var message = ChatMessage.FromAssistant(response.ResponseText);
            InnerMessages.Add(new OuroMessage(message, sendAndAppend.ElementName));
        }

        return response;
    }

    #endregion
}