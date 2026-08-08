using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// The rules about attachments that hold on every provider, checked before a call is spent.
/// </summary>
/// <remarks>
/// Shared rather than duplicated per provider because these are Ouroboros' own contract, not any
/// vendor's: a file reference means the same thing whoever issued it, and both rules below would be
/// wrong to enforce differently in one provider than another. Two copies of a rule that must agree
/// is two copies that eventually will not.
/// </remarks>
internal static class AttachmentRules
{
    /// <summary>
    /// Returns a failure describing the problem, or null when the attachments are usable.
    /// </summary>
    public static OuroResponseBase? Validate(ChatOptions options, OuroProvider provider)
    {
        if (options.Attachments is not { Count: > 0 } attachments)
            return null;

        // Attachments are mounted into the execution container, so without one there is nowhere for
        // them to go. Sending anyway produces a model that answers as though the file were never
        // mentioned, which reads as the model being obtuse rather than the request being wrong.
        if (!options.ServerTools.HasFlag(OuroServerTools.CodeExecution))
            return new OuroResponseInternalError(
                "Attachments were supplied without OuroServerTools.CodeExecution. Files are mounted "
                + "into the execution container, so without one there is nowhere for them to go.");

        foreach (var attachment in attachments)
        {
            if (attachment.Provider != provider)
                return new OuroResponseInternalError(
                    $"Attachment '{attachment.Id}' belongs to {attachment.Provider}, but this call "
                    + $"routes to {provider}. File references are not portable between providers - "
                    + "upload the file to the one serving the call.");
        }

        return null;
    }
}
