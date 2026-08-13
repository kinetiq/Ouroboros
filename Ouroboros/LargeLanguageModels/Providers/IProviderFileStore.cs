using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// A provider's file store: getting data in, and artifacts back out.
/// </summary>
/// <remarks>
/// The execution sandboxes have no internet, so this is the only route across that boundary.
/// Without it the model can compute and print, but cannot read a spreadsheet you have or hand back
/// one it made.
///
/// Separate from <see cref="IChatProvider" /> because these are not model calls - no retry policy,
/// no attempt budget, no ProviderAttempt. They throw on failure like any other I/O.
/// </remarks>
internal interface IProviderFileStore
{
    Task<OuroFileRef> UploadAsync(byte[] content, string fileName, string? mediaType,
        CancellationToken cancellationToken);

    Task<OuroFileContent> DownloadAsync(OuroFileRef file, CancellationToken cancellationToken);

    Task DeleteAsync(OuroFileRef file, CancellationToken cancellationToken);
}
