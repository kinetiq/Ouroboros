using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
    [MaxTokens(2048)]    [Complete] Ada,
    [MaxTokens(2048)]    [Complete] Babbage,
    [MaxTokens(2048)]    [Complete] Curie,
    [MaxTokens(2048)]    [Complete] Davinci,
    [MaxTokens(2048)]    [Complete] TextAdaV1,
    [MaxTokens(2048)]    [Complete] TextBabbageV1,
    [MaxTokens(2048)]    [Complete] TextCurieV1,
    [MaxTokens(2048)]    [Complete] TextDavinciV1,
    [MaxTokens(2048)]    [Complete] TextDavinciV2,
    [MaxTokens(4096)]    [Complete] TextDavinciV3,
    [MaxTokens(4096)]    [Chat]     Gpt3_5_Turbo,
    [MaxTokens(4096)]    [Chat]     Gpt3_5_Turbo_16k,
    [MaxTokens(8192)]    [Chat]     Gpt_4,
    [MaxTokens(1047576)] [Chat]     Gpt_4_1,
    [MaxTokens(128000)]  [Chat]     Gpt_4o,
    [MaxTokens(128000)]  [Chat]     Gpt_4o_mini,
    [MaxTokens(128000)]  [Chat]     Gpt_4_turbo,
    [MaxTokens(128000)]  [Chat]     Gpt_4_turbo_2024_04_09,
    [MaxTokens(32768)]   [Chat]     Gpt_4_32k,
    [MaxTokens(100000)]  [Chat]     Gpt_o3,
    [MaxTokens(100000)]  [Chat]     Gpt_o3_mini,
    [MaxTokens(100000)]  [Chat]     Gpt_o4_mini
}

class MaxTokensAttribute : Attribute
{
    public int Tokens { get; private set; }

    public MaxTokensAttribute(int tokens)
    {
        this.Tokens = tokens;
    }
}

class ChatAttribute : Attribute
{

}

class CompleteAttribute : Attribute
{

}