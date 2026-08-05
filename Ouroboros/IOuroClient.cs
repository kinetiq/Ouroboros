using Ouroboros.Chaining;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ouroboros;

/// <summary>
/// The Ouroboros client surface. Depend on this rather than the concrete OuroClient so it can
/// be substituted in tests.
/// </summary>
public interface IOuroClient
{
    /// <summary>
    /// Fired after every ChatAsync call completes. Use for centralized logging. Exceptions thrown
    /// here do not fail the chat by default - see OnChatCompletedFailure.
    /// </summary>
    Func<ChatCompletedArgs, Task>? OnChatCompleted { get; set; }

    /// <summary>
    /// What to do when OnChatCompleted throws: HookFailurePolicy.Log (the default), .Throw,
    /// .Ignore, or .Handle(yourHandler).
    /// </summary>
    HookFailurePolicy OnChatCompletedFailure { get; set; }

    Dialog CreateDialog();

    Dialog CreateDialog(string promptName);

    Dialog CreateDialog(DialogOptions options);

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    Task<OuroResponseBase> ChatAsync(List<OuroMessage> messages, ChatOptions? options = null);

    /// <summary>
    /// Configures a default model that will be used for all chats initiated from this client,
    /// unless overridden by passing in a model via ChatOptions.
    /// </summary>
    void SetDefaultChatModel(OuroModels model, OuroReasoningEffort? reasoningEffort = null);
}
