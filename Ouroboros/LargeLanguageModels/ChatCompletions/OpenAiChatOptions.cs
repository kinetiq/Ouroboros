#nullable enable

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// Settings that only mean something on OpenAI. Reached via <see cref="ChatOptions.OpenAi" />.
/// </summary>
/// <remarks>
/// Everything both providers honour - model, token ceiling, reasoning effort, timeouts, structured
/// output - stays on ChatOptions itself, so a call carries it whichever provider serves it. Only
/// what is genuinely proprietary lives here.
///
/// The dividing line is the <em>concept</em>, not who implements it today. Stop sequences are a
/// universal idea that OpenAI's Responses API happens to lack, so they stay on the shared surface
/// and fail loudly here; moving them would bake a temporary API gap into the public API and make
/// undoing it a breaking change.
///
/// Splitting the surfaces this way means each mapper reads only its own block, so a setting meant
/// for one provider cannot silently reach the other - and both can be populated at once, which is
/// what a request able to run on either provider will need.
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
