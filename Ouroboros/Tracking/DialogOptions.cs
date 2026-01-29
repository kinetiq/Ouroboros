using System;

namespace Ouroboros.Tracking;

/// <summary>
/// Options for creating a Dialog with tracking support.
/// </summary>
public class DialogOptions
{
    /// <summary>
    /// Name of the prompt for logging purposes. If not specified, will be inferred from the system message.
    /// </summary>
    public string? PromptName { get; set; }

    /// <summary>
    /// Session tracker for grouping related prompts across multiple dialogs.
    /// </summary>
    public SessionTracker? Session { get; set; }

    /// <summary>
    /// Chain tracker for grouping prompts within this dialog. Auto-generated if not provided.
    /// </summary>
    public ChainTracker? Chain { get; set; }

    /// <summary>
    /// Convenience property to get the SessionId from the Session tracker.
    /// </summary>
    public Guid? SessionId => Session?.SessionId;

    /// <summary>
    /// Convenience property to get the ChainId from the Chain tracker.
    /// </summary>
    public Guid? ChainId => Chain?.ChainId;
}
