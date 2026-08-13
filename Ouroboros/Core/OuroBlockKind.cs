namespace Ouroboros.Core;

/// <summary>
/// Discriminator for <see cref="OuroContentBlock" />.
/// </summary>
/// <remarks>
/// Values are explicit and Unknown is 0, matching the convention in OuroModels: anything that
/// persists the numeric value stays stable across versions, and a block this version does not
/// model degrades to Unknown rather than throwing.
/// </remarks>
public enum OuroBlockKind
{
    /// <summary>
    /// The provider sent a block type this version of Ouroboros does not model.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Text the model produced.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Provider-side code execution - the code, and what running it produced.
    /// </summary>
    CodeExecution = 2
}
