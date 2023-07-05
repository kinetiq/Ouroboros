using System;
using Ouroboros.Chaining.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.Chaining;

public class ChatContext
{
    private readonly OuroClient Client;
    private readonly List<OuroMessage> InnerMessages = new();

    /// <summary>
    /// Without this, there could be an extra request at the end
    /// of the command buffer if the last command was AskAndAppend.
    /// </summary>
    private bool IsAllMessagesSent = true;

    private CompleteResponseBase? LastResponse;

    public List<IChatCommand> Commands { get; set; } = new();
    public bool HasErrors { get; set; } = false;
    public string LastError { get; set; } = "";

    #region Commands
    public ChatContext SetSystem(string prompt)
    {
        Commands.Add(new SetSystemMessage(prompt));

        return this;
    }

    public ChatContext AddAssistantMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddAssistantMessage(prompt, elementName));

        return this;
    }

    public ChatContext AddUserMessage(string prompt, string elementName = "")
    {
        Commands.Add(new AddUserMessage(prompt, elementName));

        return this;
    }

    public ChatContext AskAndAppend(string elementName = "")
    {
        Commands.Add(new AskAndAppend(elementName));

        return this;
    }

    public ChatContext RemoveLast()
    {
        Commands.Add(new RemoveLast());

        return this;
    }

    public ChatContext RemoveStartingAt(int index)
    {
        Commands.Add(new RemoveStartingAtIndex(index));

        return this;
    }

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
    private async Task<CompleteResponseBase> ExecuteCommands()
    {
        LastResponse = null;

        foreach (var command in Commands)
        {
            switch (command)
            {
                case AskAndAppend askAndAppend:
                    var response = await SendMessages();
                    LastResponse = response;

                    if (response.Success)
                    {
                        var message = ChatMessage.FromAssistant(response.ResponseText);
                        InnerMessages.Add(new OuroMessage(message, askAndAppend.ElementName));
                    }
                    else
                    {
                        return response;
                    }

                    break;
                case SetSystemMessage systemMessage:
                    IsAllMessagesSent = false;

                    // Remove any existing system message and drop this at position 0.
                    if (InnerMessages.Any() && InnerMessages[0].Role == StaticValues.ChatMessageRoles.System)
                    {
                        InnerMessages.RemoveAt(0);
                    }

                    InnerMessages.Insert(0, systemMessage.ToOuroMessage());

                    break;
                case AddAssistantMessage assistantMessage:
                    IsAllMessagesSent = false;

                    InnerMessages.Add(assistantMessage.ToOuroMessage());
                    break;
                case AddUserMessage userMessage:
                    IsAllMessagesSent = false;

                    InnerMessages.Add(userMessage.ToOuroMessage());
                    break;
                case RemoveLast removeLast:
                    if (!InnerMessages.Any())
                        break;

                    InnerMessages.RemoveAt(InnerMessages.Count - 1);
                    break;
                case RemoveStartingAtIndex removeStartingAtIndex:
                    if (!InnerMessages.Any())
                        break;

                    InnerMessages.RemoveRange(removeStartingAtIndex.Index, InnerMessages.Count - removeStartingAtIndex.Index);
                    break;
                case RemoveStartingAtElement removeStartingAtElement:
                    if (!InnerMessages.Any())
                        break;

                    var index = InnerMessages.FindIndex(x => x.ElementName == removeStartingAtElement.ElementName);

                    if (index != -1)
                        InnerMessages.RemoveRange(index, InnerMessages.Count - index);

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

    /// <summary>
    /// Sends all messages in the context to the OpenAI API.
    /// If there are any errors, the HasErrors property will be set to true and
    /// the LastError property will be set to the error message.
    /// </summary>
    private async Task<CompleteResponseBase> SendMessages()
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

    public async Task<string> AskToString()
    {
        var response = await ExecuteCommands();

        // This works even if there are errors.
        return response.ResponseText;
    }

    public async Task<CompleteResponseBase> AskToResponse()
    {
        return await ExecuteCommands();
    }

    public ChatContext(OuroClient client)
    {
        Client = client;
    }
}
