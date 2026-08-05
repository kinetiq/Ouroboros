using Ouroboros.Core;
using System.Collections.Generic;
using System.Linq;

namespace Ouroboros.Responses;

public class OuroResponseSuccess : OuroResponseBase
{
    public string Model { get; set; } = "";

    /// <summary>
    /// Used with Structured Outputs. If you specify a ResponseType in the options, this will be the
    /// deserialized result.
    /// </summary>
    public object? ResponseObject { get; set; }

    /// <summary>
    /// The response broken into its parts, in the order the model produced them. An ordinary chat
    /// turn is a single text block; provider-side tools contribute their own.
    /// </summary>
    /// <remarks>
    /// Empty rather than null on a hand-built response, so consumers can enumerate unconditionally.
    /// Reading <see cref="OuroResponseBase.ResponseText" /> remains correct for anyone who only
    /// wants what the model said - see the invariant on the block-taking constructor.
    /// </remarks>
    public IReadOnlyList<OuroContentBlock> Content { get; init; } = [];

    /// <summary>
    /// Why generation stopped. See <see cref="IsComplete" /> for the common case.
    /// </summary>
    public OuroStopReason StopReason { get; init; } = OuroStopReason.Unknown;

    /// <summary>
    /// False only when the response is known to have been cut short. An unrecognised or absent
    /// stop reason counts as complete: truncation has to be positively reported, never inferred
    /// from silence.
    /// </summary>
    public bool IsComplete => StopReason is not (OuroStopReason.MaxTokens or OuroStopReason.Paused);

    /// <summary>
    /// The code executions in <see cref="Content" />, for callers who want the outcomes without
    /// walking a heterogeneous list. A projection, not stored state - it cannot fall out of sync.
    /// </summary>
    public IEnumerable<OuroCodeExecutionBlock> CodeExecutions => Content.OfType<OuroCodeExecutionBlock>();

    public OuroResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
        ResponseObject = null;
    }

    /// <summary>
    /// Builds a response from its blocks, deriving ResponseText from them.
    /// </summary>
    /// <remarks>
    /// The invariant: for a library-produced response, ResponseText is the text blocks joined with
    /// a blank line and trimmed. Non-text blocks contribute nothing - code execution stdout is
    /// deliberately absent, so a caller who ignores blocks still gets exactly the model's prose and
    /// nothing else.
    ///
    /// Internal because consumers receive responses, they do not assemble them from blocks. It also
    /// means the invariant only has to hold where the library builds the object; ResponseText stays
    /// settable for the hand-built stubs tests rely on.
    /// </remarks>
    internal OuroResponseSuccess(IReadOnlyList<OuroContentBlock> content)
        : this(JoinText(content))
    {
        Content = content;
    }

    private static string JoinText(IReadOnlyList<OuroContentBlock> content)
    {
        return string.Join("\n\n", content.OfType<OuroTextBlock>().Select(block => block.Text)).Trim();
    }
}
