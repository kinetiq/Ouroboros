using System;

namespace Ouroboros.Core;

/// <summary>
/// A file fetched back from a provider: the bytes, plus enough to save them sensibly.
/// </summary>
/// <remarks>
/// The metadata travels with the content deliberately. A caller downloading a chart the model just
/// produced has no other way to know it is a PNG rather than a spreadsheet - the file id says
/// nothing, and guessing from the bytes is worse than asking.
/// </remarks>
public sealed record OuroFileContent(byte[] Content)
{
    /// <summary>
    /// The provider's name for the file, where it reported one.
    /// </summary>
    public string? FileName { get; init; }

    public string? MediaType { get; init; }

    public int SizeBytes => Content.Length;
}
