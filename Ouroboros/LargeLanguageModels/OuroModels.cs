using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
	// 0 is intentionally unused. It was Gpt_3_5_Turbo_Instruct, removed in 5.0 along with the
	// legacy completions endpoint. Values are pinned so a future removal can't silently
	// renumber the rest — anything persisting the underlying int would shift without warning.
	// Do not reuse 0: leaving it undefined makes default(OuroModels) fail loudly.
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5			= 1,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_1			= 2,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_2			= 3,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_mini		= 4,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_nano		= 5,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4			= 6,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4_mini	= 7,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4_nano	= 8,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_5			= 9,

	// Anthropic. Reasoning here means adaptive thinking driven by output_config.effort rather than
	// OpenAI's reasoning_effort, but it is the same knob from a caller's point of view.
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Opus_5	= 10,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Opus_4_8	= 11,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Sonnet_5	= 12,
	[MaxTokens(200000,  64000)]		    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Haiku_4_5 = 13
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

class ReasoningAttribute : Attribute
{
}

/// <summary>
/// Which vendor's API serves this model.
/// </summary>
/// <remarks>
/// An attribute rather than a switch, matching MaxTokens and Reasoning: adding a model means adding
/// one line here, and nothing anywhere else has to grow a new case to route it.
/// </remarks>
class ProviderAttribute(OuroProvider provider) : Attribute
{
    public OuroProvider Provider { get; private set; } = provider;
}
