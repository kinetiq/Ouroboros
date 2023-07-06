using System;

namespace Ouroboros.LargeLanguageModels;

public enum OuroModels
{
    [MaxTokens(2048)] [Complete] Ada,
    [MaxTokens(2048)] [Complete] Babbage,
    [MaxTokens(2048)] [Complete] Curie,
    [MaxTokens(2048)] [Complete] Davinci,
    [MaxTokens(2048)] [Complete] TextAdaV1,
    [MaxTokens(2048)] [Complete] TextBabbageV1,
    [MaxTokens(2048)] [Complete] TextCurieV1,
    [MaxTokens(2048)] [Complete] TextDavinciV1,
    [MaxTokens(2048)] [Complete] TextDavinciV2,
    [MaxTokens(4096)] [Complete] TextDavinciV3,
    [MaxTokens(4096)] [Chat]     ChatGpt3_5Turbo,
    [MaxTokens(8192)] [Chat]     Gpt_4
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