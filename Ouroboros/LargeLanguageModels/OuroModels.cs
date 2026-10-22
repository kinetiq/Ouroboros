using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
	// 0 is intentionally unused. It was Gpt_3_5_Turbo_Instruct, removed in 5.0 along with the
	// legacy completions endpoint. Values are pinned so a future removal can't silently
	// renumber the rest — anything persisting the underlying int would shift without warning.
	// Do not reuse 0: leaving it undefined makes default(OuroModels) fail loudly.
	//
	// Retiring models are marked [Obsolete] rather than removed. Removal waits for a major version,
	// and the warning gives callers the date and the replacement while there is still time to move.
	[Obsolete("OpenAI shuts down gpt-5 (snapshot 2025-08-07) on 2026-12-11. Use Gpt_5_6_Sol.")]
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5			= 1,
	[Obsolete("OpenAI shuts down gpt-5.1 on 2027-04-01. Use Gpt_6_1_Sol.")]
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_1			= 2,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_2			= 3,
	[Obsolete("OpenAI shuts down gpt-5-mini (snapshot 2025-08-07) on 2026-12-11. Use Gpt_5_6_Terra.")]
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_mini		= 4,
	[Obsolete("OpenAI shuts down gpt-5-nano (snapshot 2025-08-07) on 2026-12-11. Use Gpt_5_6_Luna.")]
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_nano		= 5,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4			= 6,
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4_mini	= 7,
	[Obsolete("OpenAI shuts down gpt-5.4-nano on 2027-04-01. Use Gpt_6_Luna.")]
	[MaxTokens(400000,  128000)]		[Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_4_nano	= 8,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_5			= 9,

	// Values are pinned, so these pick up after the Anthropic block below rather than
	// continuing 9, 10, 11. Grouping here is by vendor; the numbers just record arrival order.
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_6_Astra		= 14,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_6_Sol		= 15,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_6_Terra	= 16,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_5_6_Luna	= 17,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_6_Luna		= 20,
	[MaxTokens(1050000, 128000)]	    [Reasoning]		[Provider(OuroProvider.OpenAi)]		Gpt_6_1_Sol		= 21,

	// Anthropic. Reasoning here means adaptive thinking driven by output_config.effort rather than
	// OpenAI's reasoning_effort, but it is the same knob from a caller's point of view.
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Opus_5	= 10,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Opus_4_8	= 11,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Sonnet_5	= 12,
	// No [Reasoning]: Haiku 4.5 predates adaptive thinking and rejects it with a 400. It only takes
	// a fixed budget_tokens thinking config, which the mapper does not send.
	[MaxTokens(200000,  64000)]		    				[Provider(OuroProvider.Anthropic)]	Claude_Haiku_4_5 = 13,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Fable_5_1 = 18,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Fable_5	 = 19,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Opus_5_5	 = 22,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Sonnet_5_5 = 23,
	[MaxTokens(1000000, 128000)]	    [Reasoning]		[Provider(OuroProvider.Anthropic)]	Claude_Haiku_5_5 = 24
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
