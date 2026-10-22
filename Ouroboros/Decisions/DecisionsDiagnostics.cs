namespace Ouroboros.Decisions;

/// <summary>
/// The diagnostic id the Decisions surface is marked experimental under.
/// </summary>
/// <remarks>
/// OpenAI's Decisions API is in public beta, and its shape may change before it is generally
/// available. Marking every public type lets a consumer opt in once, with NoWarn, and know that this
/// part of the package can change in a minor release. Remove the attribute when the API is GA.
/// </remarks>
internal static class DecisionsDiagnostics
{
    public const string ExperimentalId = "OURO001";
}
