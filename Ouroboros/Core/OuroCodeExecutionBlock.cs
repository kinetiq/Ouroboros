using System.Collections.Generic;

namespace Ouroboros.Core;

/// <summary>
/// A provider-side code execution: what the model chose to run, and what came back.
/// </summary>
/// <remarks>
/// Deliberately one block rather than the request/result pair some providers put on the wire.
/// Anthropic splits it into a server_tool_use block and a separate result block correlated by id;
/// OpenAI's Responses API sends a single item carrying both. Coalescing keeps that join in the
/// mapper - one place, where provider-shaped work belongs - instead of in every consumer.
/// </remarks>
public sealed record OuroCodeExecutionBlock : OuroContentBlock
{
    public override OuroBlockKind Kind => OuroBlockKind.CodeExecution;

    /// <summary>
    /// The code or command the model asked to run. Null when the provider withheld it.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// What running it produced. Null when execution was requested but no result arrived, which
    /// is what a turn paused mid-execution looks like.
    /// </summary>
    public OuroCodeExecutionResult? Result { get; init; }
}

/// <summary>
/// The outcome of one provider-side code execution.
/// </summary>
public sealed record OuroCodeExecutionResult
{
    public string Stdout { get; init; } = "";

    public string Stderr { get; init; } = "";

    public int ExitCode { get; init; }

    /// <summary>
    /// Files the code wrote. Empty unless it produced any.
    /// </summary>
    public IReadOnlyList<OuroFileRef> Files { get; init; } = [];

    /// <summary>
    /// True when the tool itself failed - timed out, ran out of memory, was unavailable - rather
    /// than the code running and exiting non-zero. The two want different handling: one is worth
    /// retrying, the other is the model's problem to fix.
    /// </summary>
    public bool IsToolError { get; init; }
}

/// <summary>
/// A handle to a file held by the provider.
/// </summary>
/// <remarks>
/// The id is opaque and meaningful only to the client that produced it - do not persist it as if
/// it were portable. Fetch the bytes through the client that issued the call.
/// </remarks>
public sealed record OuroFileRef(string Id)
{
    public string? FileName { get; init; }

    public string? MediaType { get; init; }
}
