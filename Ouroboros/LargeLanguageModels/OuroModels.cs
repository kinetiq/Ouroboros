using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
	[MaxTokens(4096,    4096)]			[Complete]				Gpt_3_5_Turbo_Instruct,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_1,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_2,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_mini,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_nano,
	[MaxTokens(1050000, 128000)]	    [Chat]	[Reasoning]		Gpt_5_4,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_4_mini,
	[MaxTokens(400000,  128000)]		[Chat]	[Reasoning]		Gpt_5_4_nano,
	[MaxTokens(1050000, 128000)]	    [Chat]	[Reasoning]		Gpt_5_5
}

class MaxTokensAttribute(int contextWindow, int maxOutput) : Attribute
{
    /// <summary>
    /// Context window size (max input tokens).
    /// </summary>
    public int ContextWindow { get; private set; } = contextWindow;

    /// <summary>
    /// Maximum output tokens the model can generate.
    /// </summary>
    public int MaxOutput { get; private set; } = maxOutput;
}

class ChatAttribute : Attribute
{
}

class CompleteAttribute : Attribute
{
}

class ReasoningAttribute : Attribute
{
}