using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
	[MaxTokens(2048, 2048)]			[Complete]				Ada,
	[MaxTokens(2048, 2048)]			[Complete]				Babbage,
	[MaxTokens(2048, 2048)]			[Complete]				Curie,
	[MaxTokens(2048, 2048)]			[Complete]				Davinci,
	[MaxTokens(2048, 2048)]			[Complete]				TextAdaV1,
	[MaxTokens(2048, 2048)]			[Complete]				TextBabbageV1,
	[MaxTokens(2048, 2048)]			[Complete]				TextCurieV1,
	[MaxTokens(2048, 2048)]			[Complete]				TextDavinciV1,
	[MaxTokens(2048, 2048)]			[Complete]				TextDavinciV2,
	[MaxTokens(4096, 4096)]			[Complete]				TextDavinciV3,
	[MaxTokens(4096, 4096)]			[Chat]					Gpt3_5_Turbo,
	[MaxTokens(16384, 4096)]		[Chat]					Gpt3_5_Turbo_16k,
	[MaxTokens(8192, 8192)]			[Chat]					Gpt_4,
	[MaxTokens(1047576, 32768)]		[Chat]					Gpt_4_1,
	[MaxTokens(128000, 16384)]		[Chat]					Gpt_4o,
	[MaxTokens(128000, 16384)]		[Chat]					Gpt_4o_mini,
	[MaxTokens(128000, 4096)]		[Chat]					Gpt_4_turbo,
	[MaxTokens(128000, 4096)]		[Chat]					Gpt_4_turbo_2024_04_09,
	[MaxTokens(32768, 4096)]		[Chat]					Gpt_4_32k,
	[MaxTokens(200000, 100000)]		[Chat]	[Reasoning]		Gpt_5,
	[MaxTokens(200000, 100000)]		[Chat]	[Reasoning]		Gpt_5_1,
	[MaxTokens(400000, 128000)]		[Chat]	[Reasoning]		Gpt_5_2,
	[MaxTokens(200000, 100000)]		[Chat]					Gpt_5_mini,
	[MaxTokens(200000, 100000)]		[Chat]					Gpt_5_nano,
	[MaxTokens(200000, 100000)]		[Chat]	[Reasoning]		o3,
	[MaxTokens(200000, 65536)]		[Chat]	[Reasoning]		o3_mini,
	[MaxTokens(200000, 100000)]		[Chat]	[Reasoning]		o4_mini
}

class MaxTokensAttribute : Attribute
{
    /// <summary>
    /// Context window size (max input tokens).
    /// </summary>
    public int ContextWindow { get; private set; }

    /// <summary>
    /// Maximum output tokens the model can generate.
    /// </summary>
    public int MaxOutput { get; private set; }

    public MaxTokensAttribute(int contextWindow, int maxOutput)
    {
        ContextWindow = contextWindow;
        MaxOutput = maxOutput;
    }
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