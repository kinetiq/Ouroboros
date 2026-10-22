// The Decisions surface is experimental until OpenAI's API is GA. See DecisionRequestTypeTests.
#pragma warning disable OURO001

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Decisions;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Live;

/// <summary>
/// Real calls against OpenAI's Decisions API. Skipped unless OPENAI_API_KEY is set.
/// </summary>
/// <remarks>
/// The provider is hand-written from OpenAI's guide, which leaves some of the wire shape unstated.
/// The mapper marks those places UNCONFIRMED; these tests are what confirms them. Each one prints
/// the raw body, so a failure here shows what the API actually sent.
/// </remarks>
public class DecisionsLiveTests(ITestOutputHelper output)
{
    /// <summary>
    /// One question of each type against text. Also sends instructions on the choice and the
    /// score, which the guide only shows on predicates.
    /// </summary>
    [RequiresOpenAiKeyFact]
    public async Task Every_Question_Type_Round_Trips()
    {
        using var client = Build();

        var response = await client.DecideAsync(new OuroDecisionRequest
        {
            Text = "I was charged twice for order 1182 and nobody has answered my last three emails. "
                   + "I want a refund today.",
            Questions =
            [
                new OuroPredicate("wants_refund", "Does the customer ask for a refund?"),
                new OuroChoice("department",
                [
                    new OuroChoiceOption("billing", "Charges, refunds and invoices"),
                    new OuroChoiceOption("shipping", "Delivery, tracking and damaged goods"),
                    new OuroChoiceOption("technical", "Problems using the product")
                ]) { Instructions = "Which team should handle this message?" },
                new OuroScore("frustration",
                [
                    new OuroScoreLevel("Calm", "Neutral or polite tone"),
                    new OuroScoreLevel("Annoyed", "Some irritation, still cooperative"),
                    new OuroScoreLevel("Angry", "Strong frustration or repeated complaints")
                ]) { Instructions = "How frustrated is the customer?" }
            ]
        });

        var decision = AssertDecided(response);

        var refund = decision.Get<OuroPredicateAnswer>("wants_refund");
        Assert.NotNull(refund);
        Assert.InRange(refund.Probability, 0.5, 1.0);

        var department = decision.Get<OuroChoiceAnswer>("department");
        Assert.NotNull(department);
        Assert.Equal("billing", department.Choice);
        Assert.InRange(department.Confidence, 0.0, 1.0);

        var frustration = decision.Get<OuroScoreAnswer>("frustration");
        Assert.NotNull(frustration);
        Assert.InRange(frustration.Score, 0.0, 2.0);

        Assert.False(string.IsNullOrEmpty(decision.Model), "The response named no model.");
    }

    /// <summary>
    /// Text and an image in one message: the shape the guide shows only half of.
    /// </summary>
    [RequiresOpenAiKeyFact]
    public async Task Text_And_An_Image_Round_Trip()
    {
        using var client = Build();

        var response = await client.DecideAsync(new OuroDecisionRequest
        {
            Text = "A customer attached this photo of the product.",
            Images = [OuroDecisionImage.FromBytes(SolidPng(64, 64, red: 220, green: 20, blue: 20), "image/png")],
            Questions = [new OuroPredicate("mostly_red", "Is the attached image mostly red?")]
        });

        var decision = AssertDecided(response);

        var red = decision.Get<OuroPredicateAnswer>("mostly_red");
        Assert.NotNull(red);
        Assert.InRange(red.Probability, 0.5, 1.0);
    }

    /// <summary>
    /// A request the API rejects comes back as a provider error carrying OpenAI's own code and
    /// message, not a parse failure.
    /// </summary>
    [RequiresOpenAiKeyFact]
    public async Task An_Unknown_Model_Returns_The_Api_Error()
    {
        using var client = Build();

        var response = await client.DecideAsync(new OuroDecisionRequest
            {
                Text = "Hello.",
                Questions = [new OuroPredicate("greeting", "Is this a greeting?")]
            },
            new DecisionOptions { Model = "not-a-real-model", UseExponentialBackOff = false });

        output.WriteLine(response.ToString());

        var error = Assert.IsType<OuroResponseProviderError>(response);
        Assert.Equal("OpenAI", error.ErrorOrigin);
        Assert.False(string.IsNullOrEmpty(error.ErrorCode));
        Assert.DoesNotContain("HTTP", error.ErrorDetails);
    }

    private OuroDecisionResponse AssertDecided(OuroResponseBase response)
    {
        if (response is OuroResponseFailure failure)
            Assert.Fail($"{failure.ErrorOrigin} {failure.ErrorCode}: {failure.ErrorDetails} | {failure.ResponseText}");

        var decision = Assert.IsType<OuroDecisionResponse>(response);

        output.WriteLine($"{decision.DurationMs}ms prompt={decision.PromptTokens} total={decision.TotalTokenUsage}");
        output.WriteLine(decision.ResponseText);

        // An unknown answer means the API sent a type or shape the mapper does not read.
        Assert.DoesNotContain(decision.Answers, answer => answer is OuroUnknownAnswer);

        return decision;
    }

    private static OuroClient Build()
    {
        return new OuroClient(new OuroborosOptions
        {
            OpenAiApiKey = RequiresOpenAiKeyFactAttribute.ApiKey
        });
    }

    /// <summary>
    /// A solid-color PNG, built here so the test needs no image file.
    /// </summary>
    private static byte[] SolidPng(int width, int height, byte red, byte green, byte blue)
    {
        // Each row is a filter byte (0, none) followed by RGB triples.
        var raw = new byte[height * (1 + width * 3)];

        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 3);

            for (var x = 0; x < width; x++)
            {
                raw[row + 1 + x * 3] = red;
                raw[row + 2 + x * 3] = green;
                raw[row + 3 + x * 3] = blue;
            }
        }

        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 2; // color type: truecolor RGB

        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);

        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        png.Write(length);

        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(typeAndData);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(typeAndData));
        png.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in bytes)
        {
            crc ^= b;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}
