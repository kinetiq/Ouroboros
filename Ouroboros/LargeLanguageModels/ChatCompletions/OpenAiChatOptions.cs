#nullable enable

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// Settings that only mean something on OpenAI. Reached via <see cref="ChatOptions.OpenAi" />.
/// </summary>
/// <remarks>
/// Anything both providers honour stays on ChatOptions itself: model, token ceiling, reasoning
/// effort, timeouts, structured output. A call then carries it whichever provider serves it. Only
/// genuinely proprietary settings live here.
///
/// The dividing line is the <em>concept</em>, not who implements it today. Stop sequences are a
/// universal idea that Responses happens to lack, so they stay on the shared surface and fail
/// loudly here. Moving them would bake a temporary API gap into the public API.
///
/// Each mapper reads only its own block, so a setting meant for one provider cannot reach the
/// other, and both can be populated at once - which a request able to run on either provider needs.
/// </remarks>
public class OpenAiChatOptions
{
    /// <summary>
    /// A unique identifier representing your end-user, which helps OpenAI monitor and detect abuse.
    /// </summary>
    /// <remarks>
    /// Was <c>ChatOptions.User</c>. It only ever reached OpenAI's EndUserId - on an Anthropic call
    /// it was accepted and then dropped, with nothing to say so. That silent drop is the reason
    /// this split exists.
    /// </remarks>
    public string? User { get; set; }

    /// <summary>
    /// Copied rather than shared, so resolving a default onto a clone cannot write into the
    /// caller's object. See <see cref="ChatOptions.Clone" />.
    /// </summary>
    internal OpenAiChatOptions Clone()
    {
        return new OpenAiChatOptions
        {
            User = User
        };
    }
}
