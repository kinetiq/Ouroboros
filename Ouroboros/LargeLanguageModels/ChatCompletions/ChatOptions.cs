#nullable enable
using System;
using System.Collections.Generic;
using Ouroboros.Tracking;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;
public class ChatOptions
{
    /// <summary>
    /// Name of the prompt for logging purposes. If not specified, will be inferred from the system message.
    /// </summary>
    public string? PromptName { get; set; }

    /// <summary>
    /// Session tracker for grouping related prompts across multiple dialogs/calls.
    /// </summary>
    public SessionTracker? Session { get; set; }

    /// <summary>
    /// Thread tracker for grouping prompts within a single conversation.
    /// </summary>
    public ThreadTracker? Thread { get; set; }

    /// <summary>
    /// Template variables that were used to render the prompt, if any. Flows through to
    /// ChatCompletedArgs so the OnChatCompleted hook can persist inputs alongside the chat.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>
    ///     An upper bound for the number of tokens that can be generated for a completion,
    ///     including visible output tokens and reasoning tokens.
    /// </summary>
    /// <see href="https://platform.openai.com/docs/api-reference/chat/create#chat-create-max_completion_tokens" />
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    ///     Up to 4 sequences where the API will stop generating further tokens. The returned text will not contain the
    ///     stop sequence.
    /// </summary>
    public IList<string>? StopSequences { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user, which will help OpenAI to monitor and detect abuse.
    /// </summary>
    public string? User { get; set; }

    public OuroModels? Model { get; set; }

    /// <summary>
    /// If set, used automatically for structured outputs: a JSON schema is generated from the type
    /// and the response is deserialized back into it. Null means no structured output.
    /// </summary>
    public Type? ResponseType { get; set; }

    /// <summary>
    ///     Constrains effort on reasoning for reasoning models. Reducing reasoning effort can result in
    ///     faster responses and fewer tokens used on reasoning in a response.
    /// </summary>
    public OuroReasoningEffort? ReasoningEffort { get; set; }

    public bool UseExponentialBackOff { get; set; }

    public ChatOptions()
    {
        // Defaults
        MaxCompletionTokens = null;
        UseExponentialBackOff = true;
        ReasoningEffort = null;
        ResponseType = null;
    }
}
