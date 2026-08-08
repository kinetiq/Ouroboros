using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Containers;
using OpenAI.Files;
using Ouroboros.Core;

namespace Ouroboros.LargeLanguageModels.Providers.OpenAi;

/// <summary>
/// OpenAI's file store. The sibling of AnthropicFileStore, with one structural difference.
/// </summary>
/// <remarks>
/// OpenAI has two file stores, not one. Files you upload live in the account-wide store and are
/// addressed by id alone; files the code interpreter <em>writes</em> live inside the container that
/// produced them and need the container id as well. They are different endpoints on different
/// clients, and an id from one means nothing to the other.
///
/// <see cref="OuroFileRef.ContainerScope" /> is what tells them apart, which is the whole reason it
/// exists. Callers never see the distinction: they hand back the reference they were given and it
/// routes itself.
/// </remarks>
internal sealed class OpenAiFileStore(OpenAIFileClient files, ContainerClient containers) : IProviderFileStore
{
    public async Task<OuroFileRef> UploadAsync(byte[] content, string fileName, string? mediaType,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content);

        // The Stream overload specifically: the BinaryData and path overloads take no
        // CancellationToken, so using one would silently drop the caller's cancellation.
        //
        // Assistants is the purpose the code interpreter accepts. It reads as legacy naming - the
        // Assistants API is a different feature - but it is what the container mounts.
        var file = await files.UploadFileAsync(stream, fileName, FileUploadPurpose.Assistants,
            cancellationToken);

        return new OuroFileRef(file.Value.Id)
        {
            Provider = OuroProvider.OpenAi,
            FileName = file.Value.Filename,

            // Echoed back from the caller rather than read off the response: OpenAI's file object
            // carries no media type, so this is the only place it is known.
            MediaType = mediaType
        };
    }

    public async Task<OuroFileContent> DownloadAsync(OuroFileRef file, CancellationToken cancellationToken)
    {
        var bytes = file.ContainerScope is { } containerId
            ? await containers.DownloadContainerFileAsync(containerId, file.Id, cancellationToken)
            : await files.DownloadFileAsync(file.Id, cancellationToken);

        return new OuroFileContent(bytes.Value.ToArray())
        {
            FileName = file.FileName,
            MediaType = file.MediaType
        };
    }

    public async Task DeleteAsync(OuroFileRef file, CancellationToken cancellationToken)
    {
        if (file.ContainerScope is { } containerId)
        {
            await containers.DeleteContainerFileAsync(containerId, file.Id, cancellationToken);

            return;
        }

        await files.DeleteFileAsync(file.Id, cancellationToken);
    }
}
