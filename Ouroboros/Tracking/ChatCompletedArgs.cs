using System;
using System.Collections.Generic;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Responses;

namespace Ouroboros.Tracking;

/// <summary>
/// Event arguments for the OnChatCompleted hook.
/// </summary>
public record ChatCompletedArgs(
    string? PromptName,
    Guid? SessionId,
    Guid? ChainId,
    List<ChatMessage> Messages,
    OuroResponseBase Response
);
