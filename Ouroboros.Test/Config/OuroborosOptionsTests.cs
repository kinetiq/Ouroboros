using System;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Config;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Config;

/// <summary>
/// Configuration that cannot do what it claims is refused before anything runs.
/// </summary>
/// <remarks>
/// A fallback chain is bought for an emergency, so a broken one has to be reported at the moment
/// it is declared. Discovered any later means discovered during the outage it was meant to cover.
/// </remarks>
public class OuroborosOptionsTests
{
    /// <summary>
    /// The registration itself throws, so a host with a broken chain does not start.
    /// </summary>
    /// <remarks>
    /// The assertion that matters is <em>when</em>. OuroClient is registered transient, so
    /// validating only in its constructor would defer this to the first chat - long past the deploy
    /// that introduced it. Nothing is resolved here; AddOuroboros alone has to fail.
    /// </remarks>
    [Fact]
    public void Registration_Fails_When_A_Fallback_Has_No_Key()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddOuroboros(options =>
        {
            options.OpenAiApiKey = "test";
            options.FallbackModels = [OuroModels.Claude_Opus_5];
        }));

        // Names the model, the provider and the property to set - all three, because whoever reads
        // this is looking at a stack trace during a deploy.
        Assert.Contains(nameof(OuroModels.Claude_Opus_5), ex.Message);
        Assert.Contains("Anthropic", ex.Message);
        Assert.Contains(nameof(OuroborosOptions.AnthropicApiKey), ex.Message);
    }

    [Fact]
    public void Registration_Succeeds_When_Every_Fallback_Has_Its_Key()
    {
        var services = new ServiceCollection();

        services.AddOuroboros(options =>
        {
            options.OpenAiApiKey = "test";
            options.AnthropicApiKey = "test";
            options.FallbackModels = [OuroModels.Claude_Opus_5];
        });

        Assert.NotEmpty(services);
    }

    /// <summary>
    /// A client with no key at all is never intentional.
    /// </summary>
    [Fact]
    public void Registration_Fails_When_No_Key_Is_Configured()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddOuroboros(_ => { }));

        Assert.Contains("No API key", ex.Message);
    }

    /// <summary>
    /// Direct construction fails the same way, so tests and non-DI hosts get the same guarantee.
    /// </summary>
    [Fact]
    public void Constructing_A_Client_Directly_Applies_The_Same_Rules()
    {
        var options = new OuroborosOptions
        {
            OpenAiApiKey = "test",
            FallbackModels = [OuroModels.Claude_Opus_5]
        };

        Assert.Throws<InvalidOperationException>(() => new OuroClient(options));
    }

    /// <summary>
    /// An empty chain is the ordinary case and must stay silent.
    /// </summary>
    [Fact]
    public void One_Key_And_No_Chain_Is_Fine()
    {
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test" });

        Assert.NotNull(client);
    }
}
