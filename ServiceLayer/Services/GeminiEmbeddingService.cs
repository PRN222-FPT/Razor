using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class GeminiEmbeddingService(
    HttpClient httpClient,
    IOptions<GeminiOptions> options) : IEmbeddingService
{
    public async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedAsync(
        IReadOnlyList<DocumentChunkDraft> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Document embedding is unavailable because the Gemini API key is not configured.");
        }

        var embeddings = new List<IReadOnlyList<float>>(chunks.Count);
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            embeddings.Add(await EmbedTextAsync(
                chunk.Content,
                "RETRIEVAL_DOCUMENT",
                settings,
                cancellationToken));
        }

        return embeddings;
    }

    public async Task<IReadOnlyList<float>> EmbedQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Query embedding is unavailable because the Gemini API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query must not be empty.", nameof(query));
        }

        return await EmbedTextAsync(
            query.Trim(),
            "RETRIEVAL_QUERY",
            settings,
            cancellationToken);
    }

    private async Task<IReadOnlyList<float>> EmbedTextAsync(
        string text,
        string taskType,
        GeminiOptions settings,
        CancellationToken cancellationToken)
    {
        var request = new GeminiEmbedRequest(
            new GeminiContent([new GeminiPart(text)]),
            taskType,
            settings.OutputDimensionality);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{settings.EmbeddingModel}:embedContent")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("x-goog-api-key", settings.ApiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini embedding request failed with status {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
            cancellationToken: cancellationToken);
        var values = result?.Embedding?.Values;
        if (values is null || values.Count == 0)
        {
            throw new InvalidOperationException("Gemini embedding response did not include vector values.");
        }

        return values;
    }
    private sealed record GeminiEmbedRequest(
        [property: JsonPropertyName("content")] GeminiContent Content,
        [property: JsonPropertyName("task_type")] string TaskType,
        [property: JsonPropertyName("output_dimensionality")] int OutputDimensionality);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed record GeminiEmbedResponse(
        [property: JsonPropertyName("embedding")] GeminiEmbedding? Embedding);

    private sealed record GeminiEmbedding(
        [property: JsonPropertyName("values")] IReadOnlyList<float> Values);
}
