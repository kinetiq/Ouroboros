using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
    [MaxTokens(2048)]  [Complete] Ada,
    [MaxTokens(2048)]  [Complete] Babbage,
    [MaxTokens(2048)]  [Complete] Curie,
    [MaxTokens(2048)]  [Complete] Davinci,
    [MaxTokens(2048)]  [Complete] TextAdaV1,
    [MaxTokens(2048)]  [Complete] TextBabbageV1,
    [MaxTokens(2048)]  [Complete] TextCurieV1,
    [MaxTokens(2048)]  [Complete] TextDavinciV1,
    [MaxTokens(2048)]  [Complete] TextDavinciV2,
    [MaxTokens(4096)]  [Complete] TextDavinciV3,
    //[MaxTokens(4096)][Chat]     Gpt3_5Turbo_Instruct,  // Not supported by Betalgo yet 10.24.2023
    [MaxTokens(4096)]  [Chat]     Gpt3_5_Turbo,
    [MaxTokens(4096)]  [Chat]     Gpt3_5_Turbo_16k,
    [MaxTokens(8192)]  [Chat]     Gpt_4,
    [MaxTokens(32768)] [Chat]     Gpt_4_32k
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