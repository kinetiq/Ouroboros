using Ouroboros.Chaining;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Threading;
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
    /// <param name="cancellationToken">
    /// Bounds the whole call, retries included. Cancelling throws OperationCanceledException rather
    /// than returning a failure response - that is the .NET contract, and it keeps a deliberate
    /// cancellation distinguishable from the provider failing. For a per-attempt budget instead,
    /// see ChatOptions.Timeout.
    /// </param>
    Task<OuroResponseBase> ChatAsync(List<OuroMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures a default model that will be used for all chats initiated from this client,
    /// unless overridden by passing in a model via ChatOptions.
    /// </summary>
    void SetDefaultChatModel(OuroModels model, OuroReasoningEffort? reasoningEffort = null);

    /// <summary>
    /// Uploads a file to a provider's store so the model can work with it.
    /// </summary>
    /// <remarks>
    /// Attach the returned reference via ChatOptions.Attachments, and reuse it across as many calls
    /// as you like - the upload is separate precisely so it need only happen once.
    ///
    /// The provider is explicit rather than inferred from a model: files live in one vendor's store
    /// and a reference is not portable between them.
    ///
    /// Unlike ChatAsync, the file methods throw on failure rather than returning a failure
    /// response. They are plumbing, not model calls - there is no partial result to hand back, and
    /// no retry policy wrapping them.
    /// </remarks>
    Task<OuroFileRef> UploadFileAsync(byte[] content, string fileName, string? mediaType,
        OuroProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a file's bytes, along with the filename and media type the provider reports for it.
    /// </summary>
    /// <remarks>
    /// Works for files you uploaded and for files the model produced - a chart written during code
    /// execution arrives as an OuroFileRef on the code execution block.
    /// </remarks>
    Task<OuroFileContent> DownloadFileAsync(OuroFileRef file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from the provider's store.
    /// </summary>
    /// <remarks>
    /// Uploads persist until deleted and count against the account's storage, so anything
    /// long-running or repeated wants to clean up after itself.
    /// </remarks>
    Task DeleteFileAsync(OuroFileRef file, CancellationToken cancellationToken = default);
}
