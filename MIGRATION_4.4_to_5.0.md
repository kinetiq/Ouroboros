# Ouroboros Migration Guide: 4.4.0 → 5.0.0

This release removes the provider SDK (Betalgo) from Ouroboros' public API. Nothing in your code
should need to reference `Betalgo.Ranul.OpenAI` after migrating. That is the whole point of the
release: it means adding other providers later won't be another breaking change.

## Prerequisites

- **.NET 10** — unchanged from 4.4.0.
- **5.0 ships as a prerelease first** (currently `5.0.0-beta.2`). Reference it explicitly —
  `<PackageReference Include="OuroborosAI.Core" Version="5.0.0-beta.2" />` — and note that a
  floating `5.*` will **not** resolve prereleases; you need `5.*-*` if you want to float.
- Dependency changes:
  - `Microsoft.ML.Tokenizers` → **2.0.0** (new)
  - `Microsoft.ML.Tokenizers.Data.O200kBase` → **2.0.0** (new)
  - `Microsoft.Extensions.DependencyInjection.Abstractions` → **10.0.9** (was transitive, now explicit)
  - `Betalgo.Ranul.OpenAI` → 9.2.6 (unchanged, now an internal implementation detail)

---

## Breaking Changes

### 1. Chat messages use `OuroMessage`, not Betalgo's `ChatMessage`

`OuroMessage` is a record with a `Role` and `Content`. The factory names match what you were
using, so most call sites are a find-and-replace.

**Before (4.4.0):**
```csharp
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

var messages = new List<ChatMessage>
{
    ChatMessage.FromSystem("You are a helpful assistant."),
    ChatMessage.FromUser("Hello.")
};
```

**After (5.0.0):**
```csharp
using Ouroboros.Core;

var messages = new List<OuroMessage>
{
    OuroMessage.FromSystem("You are a helpful assistant."),
    OuroMessage.FromUser("Hello.")
};
```

`OuroMessage.Content` is non-nullable, so null checks against it can go.

### 2. `MessageRoles` is now `OuroRole`

Renamed for consistency with `OuroMessage` / `OuroModels`, and the `[Description]` attributes were
dropped (nothing read them). Values are unchanged: `System`, `User`, `Assistant`.

If you were comparing against Betalgo's `ChatCompletionRole`, compare against `OuroRole` instead —
it is a real enum, so this is now a compile-time-checked comparison rather than a string one.

**Before (4.4.0):**
```csharp
if (message.Role == ChatCompletionRole.System) { ... }
```

**After (5.0.0):**
```csharp
if (message.Role == OuroRole.System) { ... }
```

### 3. `ReasoningEffort` is now `OuroReasoningEffort`

**Before (4.4.0):**
```csharp
using Betalgo.Ranul.OpenAI.Contracts.Enums;

var options = new ChatOptions { ReasoningEffort = ReasoningEffort.High };
```

**After (5.0.0):**
```csharp
using Ouroboros.LargeLanguageModels;

var options = new ChatOptions { ReasoningEffort = OuroReasoningEffort.High };
```

Values are `Low`, `Medium`, `High` — the same three the provider supports.

### 4. `TemplateBase` role methods return `Task<OuroMessage>`

`AsMessage()`, `AsSystem()`, `AsUser()`, `AsAssistant()` now return `Task<OuroMessage>`, and
`GetMessageRole()` returns `OuroRole`. If you subclass `TemplateBase` without overriding these,
you have nothing to change — only the types flowing out of them.

### 5. `ChatCompletedArgs` carries Ouroboros types, and gains `Model`

```csharp
IReadOnlyList<OuroMessage> Messages   // was List<ChatMessage>
OuroModels Model                      // new, positional, sits before ReasoningEffort
OuroReasoningEffort? ReasoningEffort  // was ReasoningEffort?
```

`Messages` is also read-only now. If your `OnChatCompleted` handler iterates messages and reads
`.Role` / `.Content`, it keeps working — but `.Role` is an enum, not a string.

`Model` is new and non-nullable: it reports the model the request actually ran on, with the client
default already applied, so it is always populated even when the caller left `ChatOptions.Model`
null. It exists mainly so a logging hook can pass the right model to `TokenCount` (see item 7) —
previously there was no way to know from inside the hook what had run.

Because the record is positional, a handler that destructures it positionally needs the extra
member; one that reads named properties does not.

### 6. The text-completions API is gone

`gpt-3.5-turbo-instruct` was the only model that used it, and OpenAI's `/v1/completions` endpoint
is frozen. Use `ChatAsync`.

**Removed items:**
- `OuroClient.CompleteAsync`
- `OuroClient.SetDefaultCompletionModel`
- `CompleteOptions`
- `Constants.DefaultCompletionModel`
- `OuroModels.Gpt_3_5_Turbo_Instruct`

### 7. `OuroClient.TokenCount` requires a model

It was using GPT-3 BPE, which produced wrong counts for every model this library supports. It now
uses the correct encoding for the model you pass.

**Before (4.4.0):**
```csharp
var tokens = OuroClient.TokenCount(text);
```

**After (5.0.0):**
```csharp
var tokens = OuroClient.TokenCount(text, OuroModels.Gpt_5_4_mini);
```

Expect different — and correct — numbers. If you store token counts, historical rows were wrong.

`OuroClient.Tokenize` (which returned `List<int>`) was removed; it had no callers.

### 8. `OuroResponseOpenAiError` is now `OuroResponseProviderError`

The error origin is a constructor argument rather than hard-coded, so other providers can use it.
The constructor is now internal — you receive this type, you don't construct it.

```csharp
if (response is OuroResponseProviderError error) { ... }  // was OuroResponseOpenAiError
```

`ErrorOrigin`, `ErrorCode` and `ErrorDetails` are unchanged.

### 9. `ChatOptions` lost the sampling parameters

Every model this library supports is a GPT-5 reasoning model, and those reject non-default
sampling values with a 400. These properties were either being rejected by the API or silently
dropped before they ever reached it.

**Removed:** `Temperature`, `TopP`, `FrequencyPenalty`, `PresencePenalty`, `LogitBias`, `BestOf`,
`Suffix`, `ResponseFormat`.

Use `ReasoningEffort` to influence output instead.

### 10. `Stop` and `StopAsList` are now `StopSequences`

Two properties for one concept, inherited from the provider's request shape.

**Before (4.4.0):**
```csharp
new ChatOptions { StopAsList = new List<string> { "END" } }
```

**After (5.0.0):**
```csharp
new ChatOptions { StopSequences = new List<string> { "END" } }
```

### 11. `ChatOptions.ResponseType` is nullable; `NoType` is gone

`null` now means "no structured output" — the `NoType` sentinel served no other purpose.

**Before (4.4.0):**
```csharp
if (options.ResponseType == typeof(NoType)) { ... }
```

**After (5.0.0):**
```csharp
if (options.ResponseType is null) { ... }
```

### 12. `OuroClient.GetInnerClient` was removed

It returned the raw Betalgo client, which is exactly the coupling this release removes. If you
need direct provider access, construct your own client.

### 13. `Ouroboros.StructuredOutput.Json` is internal

`Json.GetSchema` returned provider types, so it could not stay public. `Json.ParseJson` went with
it — it was a `JsonSerializer.Deserialize` wrapper. Call `System.Text.Json` directly.

Structured output itself is unchanged: set `ChatOptions.ResponseType` and read
`OuroResponseSuccess.ResponseObject`.

### 14. `DialogExtensions.GetLast(string role)` takes an `OuroRole`

```csharp
dialog.GetLast(OuroRole.Assistant);   // was dialog.GetLast("assistant")
```

The string version was error-prone: roles were stored lowercase, so `GetLast("Assistant")` threw.

### 15. Smaller removals

- `Chaining.Storage` — supported `TemplateDialog`, which was removed in 4.0.
- `GetMaxTokensExtensions.GetMaxTokens()` — `[Obsolete]` since 4.0. Use `GetContextWindow()` or
  `GetMaxOutputTokens()`.
- `IChatCommand` is internal. Every implementation already was.
- `string.ToTitleCase()` extensions — no longer used internally.

---

## New Features

### 1. `IOuroClient`

`OuroClient` now implements `IOuroClient`, and `AddOuroboros` registers both. Depend on the
interface where you want a mocking seam.

```csharp
public class MyService(IOuroClient client)
{
    public Task<OuroResponseBase> Ask(string question) =>
        client.ChatAsync([OuroMessage.FromUser(question)]);
}
```

```csharp
var mock = new Mock<IOuroClient>();
mock.Setup(x => x.ChatAsync(It.IsAny<List<OuroMessage>>(), It.IsAny<ChatOptions>()))
    .ReturnsAsync(new OuroResponseSuccess("stubbed"));
```

`TokenCount` stays a static on `OuroClient` and is not on the interface.

### 2. `Dialog.Messages`

The conversation is now readable without reaching into internals:

```csharp
IReadOnlyList<OuroMessage> soFar = dialog.Messages;
```

### 3. `SetDefaultChatModel` actually applies the reasoning effort

It accepted a `reasoningEffort` and silently discarded it — a bug since 4.0. It now assigns it,
and the parameter is optional:

```csharp
client.SetDefaultChatModel(OuroModels.Gpt_5_4_mini);                              // effort unset
client.SetDefaultChatModel(OuroModels.Gpt_5_4, OuroReasoningEffort.High);         // effort applied
```

If you were passing an effort and wondering why nothing changed, this is why.

### 4. A failing `OnChatCompleted` hook no longer fails the chat

*(Added in `5.0.0-beta.2`.)*

The hook is awaited inline inside `ChatAsync`, so previously anything it threw propagated to the
caller — a logging bug surfaced as a failed AI call. Every consumer had to wrap their own handler
to defend against Ouroboros' internal sequencing, and forgetting once produced an outage.

Ouroboros now catches it and applies `OnChatCompletedFailure`, which takes one of four policies:

| Policy | Effect |
|---|---|
| `HookFailurePolicy.Log` | **Default.** Writes to the client's `ILogger`, chat succeeds |
| `HookFailurePolicy.Throw` | Rethrows, failing the chat — the pre-5.0 behaviour |
| `HookFailurePolicy.Ignore` | Discards it |
| `HookFailurePolicy.Handle(h)` | Calls your handler, chat succeeds |

`Log` is the default because a hook failure is usually systemic — a bad migration, a DI
misconfiguration — so `Throw` takes down every call at once rather than one. Losing a log row is
almost always the cheaper failure. Pick `Throw` only when the hook does something the caller
genuinely depends on, like persisting the conversation or enforcing a spend cap.

`Handle` is for routing errors somewhere other than `ILogger`:

```csharp
client.OnChatCompleted = args => sp.GetRequiredService<ChatLogger>().LogFromHook(args);
client.OnChatCompletedFailure = HookFailurePolicy.Handle(ex => tracker.TrackException(ex));
```

There is a second overload taking `Action<Exception, ChatCompletedArgs>`, which also hands you the
args the failed hook was given so the report can name the prompt, session and model rather than
just saying logging failed. If your handler throws, that is logged and discarded — there is
nowhere left to report to.

If you already wrap your handler in a try/catch, it is now redundant and can go.

**One caveat on the default:** `Log` is only as visible as your logging setup. `AddOuroboros`
resolves `ILogger<OuroClient>` with `GetService`, so a host with no logging configured falls back
to `NullLogger` and the policy degrades to `Ignore`. Use `Handle` if you need certainty.

### 5. Reusing a `ChatOptions` no longer leaks schema state

The structured-output schema used to be written back onto the `ChatOptions` you passed in, so
reusing one instance across calls with different `ResponseType`s could send a stale schema. The
schema is now built at request-mapping time and your options object is left alone.

As of beta.2 this goes further: `ChatAsync` works on a copy, so **nothing** is written back onto
your options. Previously a reused instance had the default model resolved into it on first use,
which meant a later `SetDefaultChatModel` never applied to it.

### 6. Anthropic as a second provider (beta.2)

Claude models route through the same client. Configure both keys via the new options overload —
only the providers you use need one:

```csharp
services.AddOuroboros(options =>
{
    options.OpenAiApiKey = configuration["OpenAI:ApiKey"];
    options.AnthropicApiKey = configuration["Anthropic:ApiKey"];
});

var response = await client.ChatAsync(messages, new ChatOptions { Model = OuroModels.Claude_Opus_5 });
```

Provider-specific caveats, all of which fail loudly rather than degrading:

- `OuroClient.TokenCount` throws for Claude models — Anthropic publishes no tokenizer. Read
  `PromptTokens` / `CompletionTokens` off the response instead.
- `ChatOptions.ResponseType` (structured output) is not implemented on Anthropic yet.

### 7. Server-side code execution and file I/O (beta.2, Claude only)

Opt in per call with `ChatOptions.ServerTools = OuroServerTools.CodeExecution`. The model writes
and runs Python in a provider-hosted sandbox; results arrive as `OuroCodeExecutionBlock` entries
on `OuroResponseSuccess.Content` (or the `CodeExecutions` convenience projection), carrying the
executed code, stdout/stderr, exit code, and references to any files it produced.

Files cross the sandbox boundary through the client: `UploadFileAsync` returns an `OuroFileRef`
you attach via `ChatOptions.Attachments`; generated files come back as refs you hand to
`DownloadFileAsync`. Uploads persist until `DeleteFileAsync`.

Code-execution turns can run for minutes — see `ChatOptions.Timeout` (per-attempt, default 10
minutes) and the `CancellationToken` parameter on `ChatAsync` (whole-call). Timeouts are no longer
retried.

### 8. Responses carry typed content blocks and a stop reason (beta.2)

`OuroResponseSuccess.Content` is the response broken into `OuroContentBlock`s; `ResponseText` is
unchanged (the text blocks joined) so existing consumers keep working. `StopReason` /
`IsComplete` make truncation visible — previously a response cut off at the token ceiling was
indistinguishable from a complete one, and truncated structured output silently parsed to a null
`ResponseObject`. **Always include a discard arm when switching over block types** — new kinds
will arrive.

---

## Quick Migration Checklist

- [ ] Replace `ChatMessage` with `OuroMessage` and `ChatMessage.From*` with `OuroMessage.From*`
- [ ] Replace `MessageRoles` / `ChatCompletionRole` comparisons with `OuroRole`
- [ ] Replace `ReasoningEffort` with `OuroReasoningEffort`
- [ ] Update `TemplateBase` consumers for the `Task<OuroMessage>` return type
- [ ] Update `OnChatCompleted` handlers for `IReadOnlyList<OuroMessage>` and `OuroReasoningEffort?`
- [ ] Remove `CompleteAsync` / `CompleteOptions` / `SetDefaultCompletionModel` / `Gpt_3_5_Turbo_Instruct` usage
- [ ] Add the model argument to `OuroClient.TokenCount` calls
- [ ] Rename `OuroResponseOpenAiError` to `OuroResponseProviderError`
- [ ] Remove `Temperature`, `TopP`, `FrequencyPenalty`, `PresencePenalty`, `LogitBias`, `BestOf`, `Suffix`, `ResponseFormat` from `ChatOptions` initializers
- [ ] Rename `Stop` / `StopAsList` to `StopSequences`
- [ ] Replace `typeof(NoType)` checks with `is null`
- [ ] Replace `Json.GetSchema` / `Json.ParseJson` calls (use `ChatOptions.ResponseType` and `System.Text.Json`)
- [ ] Change `GetLast(string)` calls to `GetLast(OuroRole)`
- [ ] Drop any remaining `using Betalgo.Ranul.OpenAI.*` from your code
- [ ] Pick an `OnChatCompletedFailure` policy, and drop any try/catch you wrapped your `OnChatCompleted` handler in
- [ ] Consider depending on `IOuroClient` and deleting any hand-rolled test seams
- [ ] Re-baseline stored token counts if you persist them
