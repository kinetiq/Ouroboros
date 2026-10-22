# What is Ouroboros?
Ouroboros makes it easy to chain and transform LLM API calls, with support for OpenAI and Anthropic behind one provider-neutral surface. You get:
 - **Multi-provider:** GPT and Claude models through the same client — pick a model, the request routes to the right vendor.
 - **Failover:** name a fallback model and a call that a provider cannot serve is retried on the other vendor. Only transient failures move down the chain; a request that is simply wrong fails where it stands. See [FAILOVER.md](FAILOVER.md).
 - **Paused turns continue themselves:** a Claude turn whose tool loop pauses part-way is carried on automatically, and comes back as one response carrying every round.
 - **Decisions (experimental):** Ask yes/no, pick-one and rating questions about text or images, and get probabilities back instead of prose. Runs on OpenAI's Decisions API in a fraction of a second. See [Decisions](#decisions-experimental) below.
 - **Server-side code execution:** The model writes and runs Python in a provider-hosted sandbox; stdout, exit codes, and generated files come back as typed blocks on the response. Works on GPT and Claude alike.
 - **File I/O:** Upload data for the model to analyze; download the artifacts (charts, spreadsheets) it produces. Same call on either provider.
 - **Clean chaining:** Fluent interface for feeding the output of one API call into the input of another.
	- Easily capture the output of one call, save it to a variable, and then use it as input downchain.
 - **Template engine:** Store your prompts as markdown right in your project, with a corresponding class for fields.
	- Store your prompts as markdown right in your project.
	- You can use a class with the same name for field injection using {{ mustache syntax }}.
	- You can also inject other templates using the same syntax.
 - **Transform model results into code:** using our Hermetic Codex and Proteus Convert (both parts of this project).
	- Detect a numbered list and auto-transform it into a List\<String> or List\<NumberedListItem>
	- Convert results directly into classes or enums.
	- Smart, recoverable detection of errors / unmappable output.
 - **Exponential backoff (retry)**: Always on, for transient outages via <a href="https://github.com/App-vNext/Polly">Polly</a> on all calls.
 - **Simple Chat API**: You can also do regular Chat Completions calls, which gives you Retry.

# How do I get started?
Check out the [Getting Started Guide](https://github.com/kinetiq/Ouroboros/wiki/Getting-Started) (this needs updating). 

# Where do I get it?
First, <a href="http://docs.nuget.org/docs/start-here/installing-nuget">install NuGet</a>. Then you can install Ouroboros from the package manager console:

>PM> Install-Package OuroborosAI.Core

# Decisions (experimental)
`IOuroDecisions` answers typed questions about a piece of text, images, or both. Each answer comes back with probabilities, so you choose where to draw the line.

```csharp
var response = await decisions.DecideAsync(new OuroDecisionRequest
{
    Text = "I was charged twice for order 1182. I want a refund today.",
    Questions =
    [
        new OuroPredicate("wants_refund", "Does the customer ask for a refund?"),
        new OuroChoice("department",
        [
            new OuroChoiceOption("billing", "Charges, refunds and invoices"),
            new OuroChoiceOption("shipping", "Delivery and tracking")
        ]),
        new OuroScore("frustration",
        [
            new OuroScoreLevel("Calm", "Neutral or polite"),
            new OuroScoreLevel("Annoyed", "Some irritation"),
            new OuroScoreLevel("Angry", "Strong frustration")
        ])
    ]
});

if (response is OuroDecisionResponse decision)
{
    var refund = decision.Get<OuroPredicateAnswer>("wants_refund")?.Probability; // 0 to 1
    var team = decision.Get<OuroChoiceAnswer>("department")?.Choice;          // "billing"
    var anger = decision.Get<OuroScoreAnswer>("frustration")?.Score;          // 0 to 2
}
```

 - Get `IOuroDecisions` from dependency injection. `AddOuroboros` registers it. It needs an OpenAI key.
 - `OuroPredicate` asks if a condition is true. `OuroChoice` picks one option. `OuroScore` rates against ordered levels. Put the levels lowest first.
 - A score can fall between two levels. 1.4 means mostly level 1, with some weight on level 2.
 - Add images with `OuroDecisionImage.FromBytes` or `OuroDecisionImage.FromUrl`.
 - A failed call returns the same error types as `ChatAsync`. A question the model declines comes back as `OuroDecisionRefusal`, and `Get<T>` returns null for it.
 - Retry and timeout work as they do on chat. Each attempt has a 30-second limit by default. Change it with `DecisionOptions.Timeout`.
 - OpenAI's Decisions API is in beta, so every Decisions type is marked `[Experimental("OURO001")]`. To use them, add `<NoWarn>$(NoWarn);OURO001</NoWarn>` to your project. These types can change in a minor release.

# Limits and Possible Contributions
Ouroboros is production-ready, but it does have limits. If you would like those limits to go away, get involved!
 - Supports OpenAI (GPT-5, GPT-5.6 and GPT-6 families, Responses API) and Anthropic (Claude, including the Fable family). Provider SDKs are internal details, so adding more providers is not a breaking change.
 - Server-side code execution, file I/O, and structured output all work on both providers. Where a capability genuinely does not exist on one of them, asking for it fails loudly rather than degrading silently - see stop sequences below.
 - Local token counting (`OuroClient.TokenCount`) is OpenAI-only — Anthropic publishes no tokenizer, so read the provider's own usage off the response instead.
 - Failover moves on transient failures only (rate limits, server faults, transient network errors, a blown attempt timeout). Each chain entry gets its own full retry budget, so a chain can run considerably longer than a single call — bound it with a `CancellationToken`.
 - You can't modify our retry policy, although you _can_ turn it off. Timeouts are per-attempt via `ChatOptions.Timeout`; whole-call cancellation via `CancellationToken`.
 - `ChatOptions.StopSequences` works on Claude but not on GPT: the Responses API has no stop parameter at all, so those requests fail loudly rather than running past where you asked them to stop.
 - Decisions is OpenAI-only, with no failover. Anthropic has no equivalent API.
 - We could use some help implementing Logging, MCP, and streaming.
