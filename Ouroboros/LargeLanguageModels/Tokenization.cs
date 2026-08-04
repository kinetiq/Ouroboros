using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.ML.Tokenizers;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Token counting for supported models.
/// </summary>
/// <remarks>
/// We resolve the encoding from our own model enum rather than handing the model *name* to
/// the tokenizer library. Name-based lookup breaks every time a provider ships a model the
/// installed library hasn't heard of; the encoding families move far more slowly.
/// </remarks>
internal static class Tokenization
{
    private const string O200KBase = "o200k_base";

    private static readonly ConcurrentDictionary<string, Lazy<Tokenizer>> Cache = new();

    internal static int CountTokens(string text, OuroModels model)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        return GetTokenizer(GetEncodingName(model))
            .CountTokens(text);
    }

    private static string GetEncodingName(OuroModels model) => model switch
    {
        // Every model we currently support is GPT-5 family, which uses o200k_base.
        OuroModels.Gpt_5 or
        OuroModels.Gpt_5_1 or
        OuroModels.Gpt_5_2 or
        OuroModels.Gpt_5_mini or
        OuroModels.Gpt_5_nano or
        OuroModels.Gpt_5_4 or
        OuroModels.Gpt_5_4_mini or
        OuroModels.Gpt_5_4_nano or
        OuroModels.Gpt_5_5 => O200KBase,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "No tokenizer encoding is mapped for this model.")
    };

    /// <summary>
    /// Tokenizers are expensive to construct (o200k_base is several MB), so we build one per
    /// encoding and reuse it.
    /// </summary>
    /// <remarks>
    /// Wrapped in a Lazy because GetOrAdd does not guarantee the factory runs only once — under
    /// concurrent first use, several callers would otherwise each build a tokenizer and all but
    /// one would be thrown away.
    /// </remarks>
    private static Tokenizer GetTokenizer(string encodingName)
        => Cache.GetOrAdd(
                encodingName,
                static name => new Lazy<Tokenizer>(
                    () => TiktokenTokenizer.CreateForEncoding(name),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
}
