using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
    [MaxTokens(2048)] Ada,
    [MaxTokens(2048)] Babbage,
    [MaxTokens(2048)] Curie,
    [MaxTokens(2048)] Davinci,
    [MaxTokens(2048)] TextAdaV1,
    [MaxTokens(2048)] TextBabbageV1,
    [MaxTokens(2048)] TextCurieV1,
    [MaxTokens(2048)] TextDavinciV1,
    [MaxTokens(2048)] TextDavinciV2,
    [MaxTokens(4096)] TextDavinciV3,
    [MaxTokens(4096)] ChatGpt3_5Turbo,
    [MaxTokens(8192)] Gpt_4
}


class MaxTokensAttribute : Attribute
{
    public int Tokens { get; private set; }

    public MaxTokensAttribute(int tokens)
    {
        this.Tokens = tokens;
    }
}