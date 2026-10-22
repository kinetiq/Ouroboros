using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Decisions;
using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers.OpenAi;

/// <summary>
/// OpenAI's Decisions API, over raw HTTP.
/// </summary>
/// <remarks>
/// One attempt per call. Retry, timeout and cancellation belong to AttemptRunner, as they do for
/// chat; this reports each outcome as a ProviderAttempt so the runner can judge it the same way.
///
/// Raw HTTP because the OpenAI .NET SDK has no Decisions client. It shares OuroClient's OpenAI
/// HttpClient, which is configured with no timeout of its own, for the reason given in OuroClient.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
internal sealed class OpenAiDecisionsProvider(HttpClient http, string apiKey, Uri? endpoint = null)
{
    public static readonly Uri DefaultEndpoint = new("https://api.openai.com/v1/decisions");

    private readonly Uri Endpoint = endpoint ?? DefaultEndpoint;

    public async Task<ProviderAttempt> SendAsync(OuroDecisionRequest request, DecisionOptions options,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(OpenAiDecisionsMappings.MapRequest(request, options), Encoding.UTF8,
                "application/json")
        };

        // Per request, never on DefaultRequestHeaders. This HttpClient also carries the OpenAI SDK's
        // own traffic, and a default Authorization header would be sent alongside the SDK's.
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Transport faults escape on purpose, as they do from the Responses provider: AttemptRunner
        // reads the exception chain and is the only thing that decides what transient means.
        using var response = await http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var (code, errorMessage) = OpenAiDecisionsMappings.MapError(body);

            var error = new OuroResponseProviderError("OpenAI", code ?? status.ToString(),
                errorMessage ?? $"HTTP {status} from the Decisions API.");

            // Rate limiting and server faults are worth another attempt; a 4xx about the request
            // itself is not - retrying just repeats it. Same rule as the Responses provider.
            return new ProviderAttempt(error, status == 429 || status >= 500);
        }

        try
        {
            return ProviderAttempt.Final(OpenAiDecisionsMappings.MapResponse(body));
        }
        // A 2xx this cannot read fails the same way on every attempt, so it is final. Most likely the
        // beta API changed shape; the message says so, so nobody hunts for a network fault.
        catch (JsonException ex)
        {
            return ProviderAttempt.Final(new OuroResponseInternalError(
                "The Decisions API returned a response Ouroboros could not read. The beta API may have "
                + "changed shape. " + ex.Message));
        }
    }
}
