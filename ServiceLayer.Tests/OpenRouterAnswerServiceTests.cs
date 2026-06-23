using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class OpenRouterAnswerServiceTests
{
    [Fact]
    public async Task GenerateAnswerAsync_ReturnsFallback_WhenNoContexts()
    {
        var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/")
        };
        var service = CreateService(httpClient);

        var answer = await service.GenerateAnswerAsync(
            new AnswerGenerationRequest("Explain OOP", []));

        Assert.Equal(
            "Khong tim thay noi dung lien quan trong tai lieu da tai len cho mon hoc nay.",
            answer);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateAnswerAsync_SendsStreamingRequestAndReturnsConcatenatedAnswer()
    {
        var handler = new RecordingHttpMessageHandler(responseBody: """
            data: {"choices":[{"delta":{"content":"Polymorphism"}}]}

            data: {"choices":[{"delta":{"content":" allows"}}]}

            data: {"choices":[{"delta":{"content":" different behaviors."}}]}

            data: [DONE]
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/")
        };
        var service = CreateService(httpClient);
        var deltas = new List<string>();

        var answer = await service.GenerateAnswerAsync(
            new AnswerGenerationRequest(
                "What is polymorphism?",
                [
                    new RetrievedChatContext(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        "OOP Lecture",
                        "PRN222",
                        "Advanced C#",
                        "OOP",
                        3,
                        "Polymorphism allows one interface to represent different concrete behaviors.",
                        0.92)
                ]),
            delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.Equal("Polymorphism allows different behaviors.", answer);
        Assert.Equal(["Polymorphism", " allows", " different behaviors."], deltas);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer test-key", request.Authorization);
        Assert.Equal("FPT UniRAG Test", request.Title);
        Assert.EndsWith("api/v1/chat/completions", request.Uri);
        Assert.Contains("Markdown", request.Body);
        Assert.Contains("tra loi truc tiep", request.Body);
        Assert.Contains("5-7 doan", request.Body);
        Assert.Contains("6-10 bullet", request.Body);
        Assert.Contains("Y chinh tu tai lieu tham khao", request.Body);

        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal("openai/gpt-4o-mini", document.RootElement.GetProperty("model").GetString());
        Assert.True(document.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(0.2, document.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(2400, document.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GenerateAnswerAsync_ContinuesWhenStreamStopsWithLengthFinishReason()
    {
        var handler = new SequencedHttpMessageHandler(
            new RecordedResponse(HttpStatusCode.OK, """
                data: {"choices":[{"delta":{"content":"Phan mo dau."},"finish_reason":null}]}

                data: {"choices":[{"delta":{"content":" Con dang bi cat"},"finish_reason":"length"}]}

                data: [DONE]
                """),
            new RecordedResponse(HttpStatusCode.OK, """
                data: {"choices":[{"delta":{"content":" va day du hon."},"finish_reason":"stop"}]}

                data: [DONE]
                """));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/")
        };
        var service = CreateService(httpClient);
        var deltas = new List<string>();

        var answer = await service.GenerateAnswerAsync(
            new AnswerGenerationRequest(
                "What is polymorphism?",
                [
                    new RetrievedChatContext(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        "OOP Lecture",
                        "PRN222",
                        "Advanced C#",
                        "OOP",
                        3,
                        "Polymorphism allows one interface to represent different concrete behaviors.",
                        0.92)
                ]),
            delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.Equal("Phan mo dau. Con dang bi cat va day du hon.", answer);
        Assert.Equal(
            [
                "Phan mo dau.",
                " Con dang bi cat",
                " va day du hon."
            ],
            deltas);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Tiep tuc phan con lai", handler.Requests[1].Body);
        Assert.Contains("Khong lap lai noi dung da tra loi", handler.Requests[1].Body);
    }

    [Fact]
    public async Task GenerateAnswerAsync_ContinuesWhenStreamEndsWithoutDoneMarker()
    {
        var handler = new SequencedHttpMessageHandler(
            new RecordedResponse(HttpStatusCode.OK, """
                data: {"choices":[{"delta":{"content":"Phan mo dau."},"finish_reason":null}]}

                data: {"choices":[{"delta":{"content":" Con dang chua xong"},"finish_reason":null}]}
                """),
            new RecordedResponse(HttpStatusCode.OK, """
                data: {"choices":[{"delta":{"content":" va duoc tiep tuc."},"finish_reason":"stop"}]}

                data: [DONE]
                """));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/")
        };
        var service = CreateService(httpClient);
        var deltas = new List<string>();

        var answer = await service.GenerateAnswerAsync(
            new AnswerGenerationRequest(
                "What is polymorphism?",
                [
                    new RetrievedChatContext(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        "OOP Lecture",
                        "PRN222",
                        "Advanced C#",
                        "OOP",
                        3,
                        "Polymorphism allows one interface to represent different concrete behaviors.",
                        0.92)
                ]),
            delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.Equal("Phan mo dau. Con dang chua xong va duoc tiep tuc.", answer);
        Assert.Equal(
            [
                "Phan mo dau.",
                " Con dang chua xong",
                " va duoc tiep tuc."
            ],
            deltas);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GenerateAnswerAsync_IncludesOpenRouterErrorBody_WhenRequestFails()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """
            {
              "error": {
                "message": "Invalid model"
              }
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/")
        };
        var service = CreateService(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAnswerAsync(
                new AnswerGenerationRequest(
                    "What is polymorphism?",
                    [
                        new RetrievedChatContext(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "OOP Lecture",
                            "PRN222",
                            "Advanced C#",
                            "OOP",
                            3,
                            "Content",
                            0.92)
                    ])));

        Assert.Contains("status 400", exception.Message);
        Assert.Contains("Invalid model", exception.Message);
    }

    private static OpenRouterAnswerService CreateService(HttpClient httpClient)
    {
        return new OpenRouterAnswerService(
            httpClient,
            Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                Model = "openai/gpt-4o-mini",
                AppName = "FPT UniRAG Test",
                Temperature = 0.2,
                MaxOutputTokens = 2400
            }),
            Options.Create(new RagChatOptions
            {
                MaxContextCharacters = 6000
            }));
    }

    private sealed class RecordingHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseBody = null) : HttpMessageHandler
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
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                request.Headers.TryGetValues("X-Title", out var values)
                    ? values.Single()
                    : string.Empty,
                body));

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody ?? """
                    data: {"choices":[{"delta":{"content":"Polymorphism allows different behaviors."}}]}

                    data: [DONE]
                    """)
            };
        }
    }

    private sealed record RecordedRequest(
        string Uri,
        string Authorization,
        string Title,
        string Body);

    private sealed class SequencedHttpMessageHandler(params RecordedResponse[] responses) : HttpMessageHandler
    {
        private readonly Queue<RecordedResponse> responsesQueue = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                request.Headers.TryGetValues("X-Title", out var values)
                    ? values.Single()
                    : string.Empty,
                body));

            if (responsesQueue.Count == 0)
            {
                throw new InvalidOperationException("No queued response is available for the next OpenRouter request.");
            }

            var response = responsesQueue.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body)
            };
        }
    }

    private sealed record RecordedResponse(HttpStatusCode StatusCode, string Body);
}
