using System.IO;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Core;
using Anthropic.Models.Beta.Files;
using Ouroboros.Core;
using AnthropicSdk = Anthropic;

namespace Ouroboros.LargeLanguageModels.Providers.Anthropic;

/// <summary>
/// Anthropic's file store: getting data into the execution sandbox, and artifacts back out.
/// </summary>
/// <remarks>
/// The sandbox has no internet, so this is the only route across that boundary. Without it code
/// execution can compute and print, but cannot read a spreadsheet you have or hand back one it
/// made.
///
/// Separate from AnthropicChatProvider because these are not model calls - no retry policy, no
/// attempt budget, no ProviderAttempt. They throw on failure like any other I/O.
/// </remarks>
internal sealed class AnthropicFileStore(AnthropicSdk.AnthropicClient client)
{
    public async Task<OuroFileRef> UploadAsync(byte[] content, string fileName, string? mediaType,
        CancellationToken cancellationToken)
    {
        var file = new BinaryContent
        {
            Stream = new MemoryStream(content),
            FileName = fileName
        };

        // Anthropic infers a type when none is given, but a wrong guess on a .csv or .xlsx changes
        // how the model treats it, so pass one through whenever the caller knows.
        if (!string.IsNullOrWhiteSpace(mediaType))
            file.ContentType = MediaTypeHeaderValue.Parse(mediaType);

        var metadata = await client.Beta.Files.Upload(new FileUploadParams { File = file }, cancellationToken);

        return ToRef(metadata);
    }

    public async Task<OuroFileContent> DownloadAsync(OuroFileRef file, CancellationToken cancellationToken)
    {
        var fileName = file.FileName;
        var mediaType = file.MediaType;

        // A file the model generated arrives as a bare id - the execution result block carries no
        // name or type - so fetch those. A file we uploaded already has both, and paying for a
        // round trip to learn what we told it is silly.
        if (fileName is null || mediaType is null)
        {
            var metadata = await client.Beta.Files.RetrieveMetadata(
                file.Id, new FileRetrieveMetadataParams(), cancellationToken);

            fileName ??= metadata.Filename;
            mediaType ??= metadata.MimeType;
        }

        using var response = await client.Beta.Files.Download(file.Id, new FileDownloadParams(), cancellationToken);
        using var source = await response.ReadAsStream();
        using var buffer = new MemoryStream();

        await source.CopyToAsync(buffer, cancellationToken);

        return new OuroFileContent(buffer.ToArray())
        {
            FileName = fileName,
            MediaType = mediaType
        };
    }

    public async Task DeleteAsync(OuroFileRef file, CancellationToken cancellationToken)
    {
        await client.Beta.Files.Delete(file.Id, new FileDeleteParams(), cancellationToken);
    }

    private static OuroFileRef ToRef(FileMetadata metadata)
    {
        return new OuroFileRef(metadata.ID)
        {
            Provider = OuroProvider.Anthropic,
            FileName = metadata.Filename,
            MediaType = metadata.MimeType
        };
    }
}
