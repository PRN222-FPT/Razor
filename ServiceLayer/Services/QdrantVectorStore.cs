using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class QdrantVectorStore(
    HttpClient httpClient,
    IOptions<QdrantOptions> qdrantOptions,
    IOptions<GeminiOptions> geminiOptions) : IVectorStore
{
    public async Task UpsertAsync(
        IReadOnlyList<EmbeddedDocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var options = qdrantOptions.Value;
        await EnsureCollectionAsync(options.CollectionName, cancellationToken);

        var points = chunks.Select(chunk => new QdrantPoint(
            chunk.ChunkId,
            chunk.Vector,
            new QdrantPayload(
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.SubjectId,
                chunk.ChapterId,
                chunk.DocumentTitle,
                chunk.ChunkIndex)))
            .ToList();

        using var request = CreateRequest(
            HttpMethod.Put,
            $"collections/{options.CollectionName}/points?wait=true",
            new QdrantUpsertRequest(points));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Qdrant point upsert failed with status {(int)response.StatusCode}.");
        }
    }

    public async Task DeleteByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var options = qdrantOptions.Value;
        await EnsureCollectionAsync(options.CollectionName, cancellationToken);

        using var request = CreateRequest(
            HttpMethod.Post,
            $"collections/{options.CollectionName}/points/delete?wait=true",
            new QdrantDeleteRequest(
                new QdrantFilter(
                [
                    new QdrantFieldCondition(
                        "documentId",
                        new QdrantMatchValue(documentId.ToString()))
                ])));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Qdrant point delete failed with status {(int)response.StatusCode}.");
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Vector.Count == 0)
        {
            return [];
        }

        var options = qdrantOptions.Value;
        await EnsureCollectionAsync(options.CollectionName, cancellationToken);

        using var httpRequest = CreateRequest(
            HttpMethod.Post,
            $"collections/{options.CollectionName}/points/query",
            new QdrantSearchRequest(
                request.Vector,
                request.Limit,
                false,
                true,
                new QdrantFilter(
                [
                    new QdrantFieldCondition(
                        "subjectId",
                        new QdrantMatchValue(request.SubjectId.ToString()))
                ])));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Qdrant point search failed with status {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(
            cancellationToken: cancellationToken);

        return (result?.Result?.Points ?? [])
            .Select(point => ToVectorSearchResult(point))
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();
    }

    private async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        using var getRequest = CreateRequest(HttpMethod.Get, $"collections/{collectionName}");
        using var getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
        if (getResponse.IsSuccessStatusCode)
        {
            return;
        }

        if (getResponse.StatusCode != HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Qdrant collection check failed with status {(int)getResponse.StatusCode}.");
        }

        using var createRequest = CreateRequest(
            HttpMethod.Put,
            $"collections/{collectionName}",
            new QdrantCollectionRequest(new QdrantVectorParams(geminiOptions.Value.OutputDimensionality, "Cosine")));
        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Qdrant collection creation failed with status {(int)createResponse.StatusCode}.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        var apiKey = qdrantOptions.Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("api-key", apiKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private sealed record QdrantCollectionRequest(
        [property: JsonPropertyName("vectors")] QdrantVectorParams Vectors);

    private sealed record QdrantVectorParams(
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("distance")] string Distance);

    private sealed record QdrantUpsertRequest(
        [property: JsonPropertyName("points")] IReadOnlyList<QdrantPoint> Points);

    private sealed record QdrantDeleteRequest(
        [property: JsonPropertyName("filter")] QdrantFilter Filter);

    private sealed record QdrantSearchRequest(
        [property: JsonPropertyName("query")] IReadOnlyList<float> Query,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("with_vectors")] bool WithVectors,
        [property: JsonPropertyName("with_payload")] bool WithPayload,
        [property: JsonPropertyName("filter")] QdrantFilter Filter);

    private sealed record QdrantFilter(
        [property: JsonPropertyName("must")] IReadOnlyList<QdrantFieldCondition> Must);

    private sealed record QdrantFieldCondition(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("match")] QdrantMatchValue Match);

    private sealed record QdrantMatchValue(
        [property: JsonPropertyName("value")] string Value);

    private sealed record QdrantPoint(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("vector")] IReadOnlyList<float> Vector,
        [property: JsonPropertyName("payload")] QdrantPayload Payload);

    private sealed record QdrantPayload(
        [property: JsonPropertyName("documentId")] Guid DocumentId,
        [property: JsonPropertyName("chunkId")] Guid ChunkId,
        [property: JsonPropertyName("subjectId")] Guid SubjectId,
        [property: JsonPropertyName("chapterId")] Guid ChapterId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("chunkIndex")] int ChunkIndex);

    private sealed record QdrantSearchResponse(
        [property: JsonPropertyName("result")] QdrantSearchResult? Result);

    private sealed record QdrantSearchResult(
        [property: JsonPropertyName("points")] IReadOnlyList<QdrantScoredPoint>? Points);

    private sealed record QdrantScoredPoint(
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("payload")] QdrantPayload? Payload);

    private static VectorSearchResult? ToVectorSearchResult(QdrantScoredPoint point)
    {
        var payload = point.Payload;

        return payload is null
            ? null
            : new VectorSearchResult(
                payload.ChunkId,
                payload.DocumentId,
                payload.SubjectId,
                payload.ChapterId,
                payload.Title,
                payload.ChunkIndex,
                point.Score);
    }
}
