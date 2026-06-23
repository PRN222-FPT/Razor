using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class GeminiEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedQueryAsync_UsesRetrievalQueryTaskType()
    {
        var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        var service = new GeminiEmbeddingService(
            httpClient,
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                EmbeddingModel = "gemini-embedding-001",
                OutputDimensionality = 768
            }));

        var vector = await service.EmbedQueryAsync("What is polymorphism?");

        Assert.Equal([0.1f, 0.2f], vector);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("test-key", request.ApiKey);
        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal("RETRIEVAL_QUERY", document.RootElement.GetProperty("task_type").GetString());
        Assert.Equal(
            "What is polymorphism?",
            document.RootElement.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString());
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Headers.TryGetValues("x-goog-api-key", out var values)
                    ? values.Single()
                    : string.Empty,
                body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "embedding": {
                        "values": [0.1, 0.2]
                      }
                    }
                    """)
            };
        }
    }

    private sealed record RecordedRequest(string ApiKey, string Body);
}
