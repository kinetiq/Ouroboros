using System;
using Ouroboros.Chaining.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.TextProcessing;
using Ouroboros.Responses;
using Ouroboros.Responses.Extensions;

namespace Ouroboros.Chaining;

public class ChatContext
{
    private readonly OuroClient Client;
    private readonly List<OuroMessage> InnerMessages = new();

    /// <summary>
    /// Without this, there could be an extra request at the end
    /// of the command buffer if the last command was SendAndAppend.
    /// </summary>
    private bool IsAllMessagesSent = true;

    private OuroResponseBase? LastResponse;

    public List<IChatCommand> Commands { get; set; } = new();
    public bool HasErrors { get; set; } = false;
    public string LastError { get; set; } = "";

    #region Commands

    /// <summary>
    /// Sets the system prompt. There can only be a single system prompt,
    /// so if you run this twice, it will replace the first one.
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public ChatContext SystemMessage(string prompt)
    {
        Commands.Add(new SetSystemMessage(prompt));

        return this;
    }

    /// <summary>
    /// Adds an assistant message as the next message.
    /// </summary>
    public ChatContext AddAssistantMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddAssistantMessage(prompt, elementName));

        return this;
    }

    /// <summary>
    /// Adds a user message as the next message.
    /// </summary>
    public ChatContext UserMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddUserMessage(prompt, elementName));

        return this;
    }

    /// <summary>
    /// Sends the chat payload for completion and adds the result as an Assistant message.
    /// </summary>
    public ChatContext SendAndAppend(string elementName = "")
    {
        Commands.Add(new SendAndAppend(elementName));

        return this;
    }

    /// <summary>
    /// Removes the last item in the chat payload.
    /// </summary>
    public ChatContext RemoveLast()
    {
        Commands.Add(new RemoveLast());

        return this;
    }

    /// <summary>
    /// Removes the message at index and all messages after it.
    /// </summary>
    public ChatContext RemoveStartingAt(int index)
    {
        Commands.Add(new RemoveStartingAtIndex(index));

        return this;
    }

    /// <summary>
    /// Removes the message named elementName and all messages after it.
    /// </summary>
    public ChatContext RemoveStartingAt(string elementName)
    {
        Commands.Add(new RemoveStartingAtElement(elementName));

        return this;
    }
    #endregion

    /// <summary>
    /// Executes all commands that were chained in, and returns the last response.
    /// If there are any errors, we immediately stop and return.
    /// </summary>
    private async Task<OuroResponseBase> ExecuteCommands()
    {
        LastResponse = null;

        foreach (var command in Commands)
        {
            switch (command)
            {
                case SendAndAppend sendAndAppend:
                    var response = await HandleSendAndAppend(sendAndAppend);

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
                    throw new InvalidOperationException($"Unhandled command: { nameof(command) }");
            }
        }

        // If we've already sent all messages, just return the last response.
        if (IsAllMessagesSent)
            return LastResponse!;

        // Otherwise, execute.
        return await SendMessages();
    }

    #region Command Handlers
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
        {
            InnerMessages.RemoveAt(0);
        }

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

    #region Terminators
    /// <summary>
    /// Sends the chat payload for completion and converts the result to a string.
    /// If there was an error, this will be an error message. Be sure to check the dialog
    /// for errors via Dialog.HasErrors
    /// </summary>
    public async Task<string> SendToString()
    {
        var response = await ExecuteCommands();

        // This works even if there are errors. But it might be an error message.
        return response.ResponseText;
    }

    /// <summary>
    /// Sends the chat payload for completion, then senses the list type and splits the text into a list.
    /// Works with numbered lists and lists separated by any type of newline. 
    /// </summary>
    public async Task<List<ListItem>> SendAndExtractList()
    {
        var response = await Send();

        return response.ExtractList();
    }

    /// <summary>
    /// Sends the chat payload for completion, then splits the result into a numbered list.
    /// Any item that doesn't start with an number is discarded. Note that this is different than SendAndExtractList
    /// in a few ways, including the result type, which in this case is able to include the item number (since these
    /// items are numbered).
    /// </summary>
    public async Task<List<NumberedListItem>> SendAndExtractNumberedList()
    {
        var response = await Send();

        return response.ExtractNumberedList();
    }

    /// <summary>
    /// Sends the chat payload for completion and converts the result to a ResponseBase.
    /// </summary>
    public async Task<OuroResponseBase> Send()
    {
        return await ExecuteCommands();
    }
    #endregion

    /// <summary>
    /// Sends all messages in the context to the OpenAI API.
    /// If there are any errors, the HasErrors property will be set to true and
    /// the LastError property will be set to the error message.
    /// </summary>
    private async Task<OuroResponseBase> SendMessages()
    {
        var messages = InnerMessages.Select(x => x.Message)
            .ToList();

        var response = await Client.ChatAsync(messages);

        IsAllMessagesSent = true;

        if (!response.Success)
        {
            HasErrors = true;
            LastError = response.ResponseText;
        }
        
        return response;
    }

    public ChatContext(OuroClient client)
    {
        Client = client;
    }
}