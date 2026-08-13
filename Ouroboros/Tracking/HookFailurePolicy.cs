using System;

namespace Ouroboros.Tracking;

/// <summary>
/// What Ouroboros does when the OnChatCompleted hook throws.
/// </summary>
/// <remarks>
/// The hook is awaited inline inside ChatAsync, so this is really deciding whether a logging
/// failure is allowed to fail the chat it was logging. Log is the default because that trade is
/// rarely worth making: hook failures tend to be systemic - a bad migration, a DI misconfiguration
/// - so Throw takes down every call at once rather than one.
/// </remarks>
public sealed class HookFailurePolicy
{
    internal HookFailureBehavior Behavior { get; }

    internal Action<Exception, ChatCompletedArgs>? Handler { get; }

    /// <summary>
    /// Report through the client's ILogger and return the chat response anyway. This is the default.
    /// </summary>
    /// <remarks>
    /// Only as visible as your logging setup. With no ILogger registered this degrades to Ignore,
    /// which is why Handle exists for anyone routing errors somewhere other than ILogger.
    /// </remarks>
    public static readonly HookFailurePolicy Log = new(HookFailureBehavior.Log);

    /// <summary>
    /// Let the exception propagate out of ChatAsync, failing the chat.
    /// </summary>
    /// <remarks>
    /// Defensible when the hook does something the caller genuinely depends on rather than logging
    /// - persisting the conversation, enforcing a spend cap. Understand that it converts a logging
    /// outage into a user-facing one.
    /// </remarks>
    public static readonly HookFailurePolicy Throw = new(HookFailureBehavior.Throw);

    /// <summary>
    /// Discard the exception. Nothing is recorded anywhere, so a hook that has been broken for
    /// months looks identical to one that never fails.
    /// </summary>
    public static readonly HookFailurePolicy Ignore = new(HookFailureBehavior.Ignore);

    /// <summary>
    /// Route the failure to your own handler and return the chat response anyway. Use this when
    /// errors belong somewhere other than ILogger - New Relic, Sentry, an internal tracker.
    /// </summary>
    /// <param name="handler">
    /// Anything it throws is logged and discarded - there is nowhere left to report to.
    /// </param>
    public static HookFailurePolicy Handle(Action<Exception> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new HookFailurePolicy(HookFailureBehavior.Handle, (ex, _) => handler(ex));
    }

    /// <summary>
    /// Route the failure to your own handler and return the chat response anyway.
    /// </summary>
    /// <param name="handler">
    /// Also receives the args the failed hook was given, so the report can name the prompt,
    /// session and model rather than just saying logging failed. Anything it throws is logged
    /// and discarded - there is nowhere left to report to.
    /// </param>
    public static HookFailurePolicy Handle(Action<Exception, ChatCompletedArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new HookFailurePolicy(HookFailureBehavior.Handle, handler);
    }

    private HookFailurePolicy(HookFailureBehavior behavior, Action<Exception, ChatCompletedArgs>? handler = null)
    {
        Behavior = behavior;
        Handler = handler;
    }
}

/// <summary>
/// Internal so the public surface stays the named HookFailurePolicy members. An enum here would
/// let callers write a value that has no handler attached to it.
/// </summary>
internal enum HookFailureBehavior
{
    Log,
    Throw,
    Ignore,
    Handle
}
