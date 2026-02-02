# Ouroboros Migration Guide: 3.1.0 → 4.0.0

This guide covers the breaking changes and new features when upgrading from Ouroboros 3.1.0 to 4.0.0.

## Prerequisites

- **.NET 10** is now required (previously .NET 9)
- Updated dependencies:
  - `Betalgo.Ranul.OpenAI` → 9.2.6
  - `Microsoft.Extensions.Logging.Abstractions` → 10.0.2
  - `Polly` → 8.6.5
  - `Scriban` → 6.5.2

---

## Breaking Changes

### 1. TemplateDialog Removed

The entire `TemplateDialog` class and related infrastructure has been removed. If you were using `TemplateDialog`, migrate to the standard `Dialog` class.

**Before (3.1.0):**
```csharp
var templateDialog = client.CreateTemplateDialog();
await templateDialog
    .Send(myTemplate)
    .StoreOutputAs("result")
    .Execute();
```

**After (4.0.0):**
```csharp
var dialog = client.CreateDialog();
await dialog
    .SystemMessage(myTemplate)
    .SendAndAppend()
    .StoreOutputAs("result")
    .Execute();
```

**Removed items:**
- `OuroClient.CreateTemplateDialog()`
- `OuroClient.SetTemplateEndpoint()`
- `OuroClient.SendTemplateAsync()`
- `ITemplateEndpoint` interface
- `TemplateRequestHandler`

### 2. ElementName Parameter Removed

The `elementName` parameter has been removed from all message methods. Use `StoreOutputAs()` for variable storage instead.

**Before (3.1.0):**
```csharp
dialog
    .UserMessage("Hello", "greeting")
    .AssistantMessage("Hi there", "response")
    .SendAndAppend("final");
```

**After (4.0.0):**
```csharp
dialog
    .UserMessage("Hello")
    .AssistantMessage("Hi there")
    .SendAndAppend()
    .StoreOutputAs("final");
```

### 3. RemoveStartingAt(elementName) Removed

The `RemoveStartingAt(string elementName)` method has been removed. Use `RemoveStartingAt(int index)` instead.

**Before (3.1.0):**
```csharp
dialog.RemoveStartingAt("myElement");
```

**After (4.0.0):**
```csharp
dialog.RemoveStartingAt(3); // Use index-based removal
```

### 4. SetDefaultChatModel Signature Changed

The method now requires a `ReasoningEffort` parameter.

**Before (3.1.0):**
```csharp
client.SetDefaultChatModel(OuroModels.Gpt_4o);
```

**After (4.0.0):**
```csharp
client.SetDefaultChatModel(OuroModels.Gpt_5_mini, ReasoningEffort.Medium);
// Or null for non-reasoning models:
client.SetDefaultChatModel(OuroModels.Gpt_4o, null);
```

### 5. Default Chat Model Changed

The default chat model has changed from `Gpt_4_1` to `Gpt_5_mini`.

### 6. ReasoningEffort Enum Location Changed

The `ReasoningEffort` enum has moved from `ChatCompletionCreateRequest.ReasoningEfforts` to `Betalgo.Ranul.OpenAI.Contracts.Enums.ReasoningEffort`.

**Before (3.1.0):**
```csharp
using static Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatCompletionCreateRequest;
options.ReasoningEffort = ReasoningEfforts.Medium;
```

**After (4.0.0):**
```csharp
using Betalgo.Ranul.OpenAI.Contracts.Enums;
options.ReasoningEffort = ReasoningEffort.Medium;
```

### 7. ChatMessageRoles → ChatCompletionRole

Internal role checking now uses `ChatCompletionRole` enum instead of `StaticValues.ChatMessageRoles`.

---

## New Features

### 1. Session & Thread Tracking

Track prompts across sessions and threads for logging and analytics.

```csharp
// Create trackers
var session = Tracker.CreateSession();
var thread = Tracker.CreateThread();

// Create dialog with tracking
var dialog = client.CreateDialog(new DialogOptions
{
    PromptName = "MyPrompt",
    Session = session,
    Thread = thread
});

// Or pass prompt name directly
var dialog = client.CreateDialog("MyPromptName");
```

### 2. Entity Tagging

Tag sessions and threads with business entities for correlation.

```csharp
var session = Tracker.CreateSession()
    .WithTag(MyEntityType.User, userId)
    .WithTag(MyEntityType.Project, projectId);

var thread = Tracker.CreateThread()
    .WithTag(MyEntityType.Task, taskId);
```

### 3. OnChatCompleted Event Hook

Subscribe to chat completions for centralized logging.

```csharp
client.OnChatCompleted = async (args) =>
{
    // args contains:
    // - PromptName, SessionId, ThreadId
    // - Messages, Response
    // - ReasoningEffort, DurationMs
    // - ThreadTags, SessionTags
    
    await LogToDatabase(args);
};
```

### 4. Response Duration Tracking

All responses now include `DurationMs` property.

```csharp
var response = await dialog.Execute();
Console.WriteLine($"Call took {response.DurationMs}ms");
```

### 5. StoreOutputAs() Method

Store the last response in a variable for later use.

```csharp
await dialog
    .SystemMessage("You are helpful")
    .UserMessage("What is 2+2?")
    .SendAndAppend()
    .StoreOutputAs("answer")
    .Execute();

var answer = dialog.Variables["answer"];
```

### 6. New Models Added

- `Gpt_5`, `Gpt_5_1`, `Gpt_5_2`
- `Gpt_5_mini`, `Gpt_5_nano`

All GPT-5 and o-series models are marked with `[Reasoning]` attribute.

### 7. MaxTokens Attribute Enhanced

Now tracks both context window and max output separately.

```csharp
// Model attributes now specify:
// [MaxTokens(contextWindow, maxOutput)]
// e.g., Gpt_5_mini: [MaxTokens(200000, 100000)]
```

---

## Quick Migration Checklist

- [ ] Upgrade to .NET 10
- [ ] Update NuGet packages
- [ ] Remove all `TemplateDialog` usage → use `Dialog`
- [ ] Remove all `elementName` parameters from message calls
- [ ] Replace `RemoveStartingAt(name)` with index-based removal
- [ ] Update `SetDefaultChatModel` calls to include `ReasoningEffort`
- [ ] Update `ReasoningEffort` enum imports
- [ ] Consider adding session/thread tracking for logging
- [ ] Consider subscribing to `OnChatCompleted` for analytics
