# Provider failover

A call that one provider cannot serve is retried on the next model you name. An OpenAI outage or a
sustained rate limit then costs you latency instead of an outage of your own.

Off by default. Nothing below happens unless you configure it.

## Configuring it

Two places, and they mean different things.

**For the whole client**, in `OuroborosOptions`:

```csharp
services.AddOuroboros(options =>
{
    options.OpenAiApiKey    = configuration["OpenAI:ApiKey"];
    options.AnthropicApiKey = configuration["Anthropic:ApiKey"];
    options.FallbackModels  = [OuroModels.Claude_Sonnet_5];
});
```

**For one call**, which overrides the client default:

```csharp
new ChatOptions
{
    Model = OuroModels.Gpt_5_4_mini,
    FallbackModels = [OuroModels.Claude_Sonnet_5]
}
```

Setting `FallbackModels = []` on a call opts it out of failover entirely, even when the client has
a default. That distinction matters — see [Calls that must not move](#calls-that-must-not-move).

`OuroClient.SetDefaultFallback(...)` does the same as the options property, at runtime. Prefer the
options property: it is validated when your host starts, while `SetDefaultFallback` runs when the
client is first resolved, which for a transient registration is the first chat rather than startup.

## What triggers it

Only a **transient** failure that survived the retry policy:

- Rate limits (429) and server faults (5xx), after the retries for that entry are exhausted
- Transient network faults — connection resets, socket errors, I/O failures
- A blown per-attempt timeout (`ChatOptions.Timeout`)

## What does not

These fail where they stand, and deliberately:

- **4xx about the request** — a bad parameter, a malformed schema
- **Auth failures** — a wrong or missing key is not a transient condition
- **Refused capabilities** — asking a provider for something it cannot do
- **Deterministic exceptions** — a mapper that throws, a `ResponseType` that will not turn into a
  schema
- **Caller cancellation** — your `CancellationToken` means stop, not try elsewhere

The rule behind the list: if the next provider would fail the same way, trying it only spends money
to reach the same answer.

## Misconfiguration

Where a mistake surfaces depends on whether it is *configuration* or *caller data*.

| Mistake | When you find out |
|---|---|
| `OuroborosOptions.FallbackModels` names a provider with no key | Host startup — `AddOuroboros` throws |
| `SetDefaultFallback` names a provider with no key | Immediately, at the call |
| No API key configured at all | Host startup |
| A call's `FallbackModels` names a provider with no key | That call returns an error naming the provider |

Configuration is known before any traffic, so it is checked before any traffic. A fallback that was
quietly discarded would be discovered during the outage it was configured for.

A call's `FallbackModels` is runtime data rather than configuration, so a bad entry there returns a
failure response instead of taking the process down.

## Options a chain cannot carry

Not every option works on every provider. `ChatOptions.StopSequences` is honoured on Claude and
refused on GPT, because OpenAI's Responses API has no stop parameter at all.

When a chain contains an entry that cannot serve the call as written:

- **The chain came from this call** — the call fails immediately, naming the option. You wrote that
  chain for this request, so a hole in it is worth stopping for.
- **The chain came from the client default** — the offending entry is dropped, with a log. A
  client-wide default is not about any one call, and configuration cannot know what options a future
  call will set.

Setting `AllowDegraded = true` says a less exact answer beats no answer: entries run without the
option they cannot express, and entries that cannot be salvaged that way are dropped.

**`AllowDegraded` never drops an attachment.** A model reasoning about a file it cannot see answers
a different question, confidently, and that comes back looking like success. A provider that cannot
see this call's files is skipped instead.

## Logging: one call, several rows

`OnChatCompleted` fires **once per attempt**, awaited in order.

Without a fallback that is exactly once per call, as it has always been. With one, a call that
failed over reports the failed attempt and the successful one separately, so a consumer logging
these keeps a record of what the first provider cost.

`ChatCompletedArgs` carries:

- `Model` — the model **this attempt** ran on, not necessarily the one requested
- `Attempt` — 1-based position in the chain
- `NextModel` — the model this failed over to, or null on the final attempt
- `DurationMs` — this attempt's duration. The response object returned from `ChatAsync` carries the
  total across every attempt instead.

If your hook writes a row per invocation, a failed-over call now writes two. That is the intent —
the failed attempt was paid for — but expect it in anything that counts rows or attributes cost.

Under `HookFailurePolicy.Throw`, every attempt's hook still runs and the first exception is raised
afterwards. Stopping at the first would lose a successful attempt's response to a fault while
logging the failed one.

## Latency

Each chain entry gets its **own full retry budget**. With `UseExponentialBackOff` on, that is five
retries from a 5s base — roughly 155 seconds of waiting per entry, before the calls themselves.

A two-entry chain can therefore run considerably longer than a single call. `ChatOptions.Timeout`
is per attempt and does not bound the chain. To bound the whole thing, pass a `CancellationToken`
to `ChatAsync`.

Cancelling mid-chain still reports the attempts that already completed to `OnChatCompleted` before
the exception surfaces — they cost real money, so they are logged rather than discarded.

## Two traps

**Reasoning effort travels with the request, not the model.** Whatever effort a call carries is used
for every entry in its chain, including an effort passed to `SetDefaultChatModel`, which was chosen
for that model rather than for the fallbacks.

**Calls that must not move.** Anything whose result is recorded against the model that produced it
needs `FallbackModels = []`. The clearest case is an evaluation harness: it runs a prompt against a
*named* model and stores the outcome against that model's row. A silent move to another provider
would label the result with the model that was requested rather than the one that answered, and
every comparison built on that data would be quietly wrong.

If a call's meaning depends on which model served it, opt it out.

## Token budgets and paused turns

`ChatOptions.MaxCompletionTokens` covers a whole turn, including the continuations Ouroboros makes
when Claude pauses a turn mid-tool-loop. Each continuation asks only for what is left of the budget,
so a turn taking four rounds still produces at most what you asked for.

This is independent of failover, but the two interact: each chain entry gets the full budget again,
because each is a separate attempt at the same question.
