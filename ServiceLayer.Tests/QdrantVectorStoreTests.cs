using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class QdrantVectorStoreTests
{
    [Fact]
    public async Task DeleteByDocumentAsync_DeletesPointsByDocumentPayload()
    {
        var documentId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:6333/")
        };
        var store = new QdrantVectorStore(
            httpClient,
            Options.Create(new QdrantOptions { CollectionName = "document_chunks" }),
            Options.Create(new GeminiOptions { OutputDimensionality = 768 }));

        await store.DeleteByDocumentAsync(documentId);

        var deleteRequest = Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Post);
        Assert.Equal(
            "http://localhost:6333/collections/document_chunks/points/delete?wait=true",
            deleteRequest.RequestUri!.ToString());

        using var document = JsonDocument.Parse(deleteRequest.Body);
        var condition = document.RootElement
            .GetProperty("filter")
            .GetProperty("must")[0];

        Assert.Equal("documentId", condition.GetProperty("key").GetString());
        Assert.Equal(documentId.ToString(), condition.GetProperty("match").GetProperty("value").GetString());
    }

    [Fact]
    public async Task SearchAsync_QueriesByVectorAndSubjectPayload()
    {
        var subjectId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(
            $$"""
            {
              "result": {
                "points": [
                  {
                    "score": 0.87,
                    "payload": {
                      "documentId": "{{documentId}}",
                      "chunkId": "{{chunkId}}",
                      "subjectId": "{{subjectId}}",
                      "chapterId": "{{chapterId}}",
                      "title": "Lecture 1",
                      "chunkIndex": 2
                    }
                  }
                ]
              }
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:6333/")
        };
        var store = new QdrantVectorStore(
            httpClient,
            Options.Create(new QdrantOptions { CollectionName = "document_chunks" }),
            Options.Create(new GeminiOptions { OutputDimensionality = 768 }));

        var results = await store.SearchAsync(
            new VectorSearchRequest([0.1f, 0.2f, 0.3f], subjectId, 5));

        var searchRequest = Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.RequestUri!.ToString().EndsWith("/points/query", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(searchRequest.Body);
        Assert.Equal(5, document.RootElement.GetProperty("limit").GetInt32());
        Assert.False(document.RootElement.GetProperty("with_vectors").GetBoolean());
        Assert.True(document.RootElement.GetProperty("with_payload").GetBoolean());
        Assert.Equal(3, document.RootElement.GetProperty("query").GetArrayLength());
        var condition = document.RootElement
            .GetProperty("filter")
            .GetProperty("must")[0];
        Assert.Equal("subjectId", condition.GetProperty("key").GetString());
        Assert.Equal(subjectId.ToString(), condition.GetProperty("match").GetProperty("value").GetString());

        var result = Assert.Single(results);
        Assert.Equal(chunkId, result.ChunkId);
        Assert.Equal(documentId, result.DocumentId);
        Assert.Equal(chapterId, result.ChapterId);
        Assert.Equal(0.87, result.Score);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public RecordingHttpMessageHandler(string responseContent = "{}")
        {
            _responseContent = responseContent;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri, body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent)
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri, string Body);
}
