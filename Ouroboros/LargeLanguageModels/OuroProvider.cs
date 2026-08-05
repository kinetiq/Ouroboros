namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Which vendor's API serves a model.
/// </summary>
/// <remarks>
/// Values are pinned and 0 is intentionally undefined, so default(OuroProvider) fails loudly rather
/// than silently routing to whichever provider happened to be listed first.
/// </remarks>
public enum OuroProvider
{
    OpenAi = 1,
    Anthropic = 2
}
