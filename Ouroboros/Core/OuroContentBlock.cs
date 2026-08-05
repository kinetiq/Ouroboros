namespace Ouroboros.Core;

/// <summary>
/// One piece of a model response. An ordinary chat turn is a single <see cref="OuroTextBlock" />;
/// provider-side tools add further blocks alongside it, in the order they occurred.
/// </summary>
/// <remarks>
/// The hierarchy is closed: the constructor is private protected, so only this assembly can
/// introduce block types. That is what keeps <see cref="Kind" /> exhaustive and lets new types
/// arrive without a breaking change.
///
/// C# does not check exhaustiveness on type patterns, so <b>a switch over blocks must include a
/// discard arm</b>. New block types will reach code compiled against an older version;
/// <see cref="OuroUnknownBlock" /> exists so that "a block I don't recognise" is an ordinary,
/// already-exercised state rather than an exception in production.
/// </remarks>
public abstract record OuroContentBlock
{
    private protected OuroContentBlock()
    {
    }

    public abstract OuroBlockKind Kind { get; }
}

/// <summary>
/// Text the model produced.
/// </summary>
public sealed record OuroTextBlock(string Text) : OuroContentBlock
{
    public override OuroBlockKind Kind => OuroBlockKind.Text;
}

/// <summary>
/// A block this version of Ouroboros does not model.
/// </summary>
/// <remarks>
/// Carries the provider's own type string purely as a diagnostic. It is not a contract, and it is
/// the single place a provider's vocabulary is deliberately allowed to surface - everywhere else,
/// provider wire shapes stop at the mapper.
/// </remarks>
public sealed record OuroUnknownBlock(string ProviderType) : OuroContentBlock
{
    public override OuroBlockKind Kind => OuroBlockKind.Unknown;
}
