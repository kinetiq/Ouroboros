using System;
using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Decisions;

/// <summary>
/// An image for a decision request to evaluate.
/// </summary>
/// <remarks>
/// There is no way to build one from an OuroFileRef. The Decisions API does not accept file ids,
/// so the image must travel inline or be fetchable from a URL.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed record OuroDecisionImage
{
    private OuroDecisionImage(string url)
    {
        Url = url;
    }

    /// <summary>
    /// The image as the API receives it: a data URL or a public web URL.
    /// </summary>
    /// <remarks>
    /// Internal because a data URL holds the whole image, base64 encoded. Public, it would turn up
    /// in logs and debugger views at many times the size of anything useful.
    /// </remarks>
    internal string Url { get; }

    /// <summary>
    /// Sends the image inline, base64 encoded.
    /// </summary>
    /// <param name="content">The image bytes.</param>
    /// <param name="mediaType">The image's media type, for example image/png.</param>
    public static OuroDecisionImage FromBytes(byte[] content, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        if (content.Length == 0)
            throw new ArgumentException("The image has no content.", nameof(content));

        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"'{mediaType}' is not an image media type. Use one that starts with image/, for "
                + "example image/png.", nameof(mediaType));
        }

        return new OuroDecisionImage($"data:{mediaType};base64,{Convert.ToBase64String(content)}");
    }

    /// <summary>
    /// Sends a link to the image. OpenAI fetches it, so the URL must be publicly reachable.
    /// </summary>
    public static OuroDecisionImage FromUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        // Checked here because the alternative is a 400 at call time. A file:// or relative URL
        // cannot be fetched by OpenAI, and a data: URL belongs in FromBytes, which checks it.
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException(
                $"'{url}' is not an http or https URL. OpenAI must be able to fetch the image. "
                + "Use FromBytes to send it inline instead.", nameof(url));
        }

        return new OuroDecisionImage(url.AbsoluteUri);
    }

    /// <summary>
    /// Shows the scheme only, so a data URL does not print a whole image.
    /// </summary>
    public override string ToString()
    {
        return Url.StartsWith("data:", StringComparison.Ordinal)
            ? $"OuroDecisionImage {{ inline, {Url.Length} chars }}"
            : $"OuroDecisionImage {{ {Url} }}";
    }
}
