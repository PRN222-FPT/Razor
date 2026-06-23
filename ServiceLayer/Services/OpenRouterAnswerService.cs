using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class OpenRouterAnswerService(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options,
    IOptions<RagChatOptions> chatOptions) : IAnswerGenerationService
{
    private const int MaxTotalAttempts = 3;
    private const string NoContextFallback =
        "Khong tim thay noi dung lien quan trong tai lieu da tai len cho mon hoc nay.";
    private const string SystemPrompt =
        "Ban la tro ly hoc tap cho sinh vien. Chi tra loi dua tren CONTEXT duoc cung cap, " +
        "khong dung kien thuc ngoai tai lieu. Tra loi bang tieng Viet, dung Markdown ro rang, " +
        "va uu tien cau tra loi day du, co nghia hon la ngan gon. " +
        "Moi cau tra loi phai co: 1) tra loi truc tiep trong 1-2 cau dau; 2) giai thich ro hon theo tung y; " +
        "3) y chinh tu tai lieu tham khao; 4) neu context du, them vi du ngan hoac luu y thuc te. " +
        "Khi context phong phu, co gang viet thanh 5-7 doan hoac 6-10 bullet ro rang de cau tra loi day du hon. " +
        "Su dung tieu de Markdown nhu ## va ###, danh sach bullet, va in dam khi can de cau tra loi de doc hon. " +
        "Neu context chua du de tra loi day du, noi ro phan thieu thong tin thay vi doan. " +
        "Khi nhac nguon, chi dung ten tai lieu/chuong/chunk da co trong CONTEXT, khong tu tao nguon.";

    public async Task<string> GenerateAnswerAsync(
        AnswerGenerationRequest request,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }

        if (request.Contexts.Count == 0)
        {
            if (onDelta is not null)
            {
                await onDelta(NoContextFallback);
            }

            return NoContextFallback;
        }

        var answer = new StringBuilder();
        var attempt = 0;
        StreamedAnswerResult streamResult;

        do
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "api/v1/chat/completions")
            {
                Content = JsonContent.Create(BuildRequest(settings, request, answer.ToString()))
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            if (!string.IsNullOrWhiteSpace(settings.AppName))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Title", settings.AppName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(settings.SiteUrl))
            {
                httpRequest.Headers.Referrer = new Uri(settings.SiteUrl, UriKind.Absolute);
            }

            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var detail = string.IsNullOrWhiteSpace(responseBody)
                    ? response.ReasonPhrase
                    : responseBody;

                throw new InvalidOperationException(
                    $"OpenRouter answer generation failed with status {(int)response.StatusCode}: {detail}");
            }

            streamResult = await ReadStreamedAnswerAsync(response, onDelta, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamResult.Answer))
            {
                answer.Append(streamResult.Answer);
            }

            attempt++;
        }
        while (ShouldContinue(streamResult, attempt));

        var finalAnswer = answer.ToString().Trim();
        return string.IsNullOrWhiteSpace(finalAnswer)
            ? "OpenRouter did not return an answer."
            : finalAnswer;
    }

    private OpenRouterChatRequest BuildRequest(
        OpenRouterOptions settings,
        AnswerGenerationRequest request,
        string previousAnswer)
    {
        var messages = new List<OpenRouterMessage>
        {
            new("system", SystemPrompt),
            new("user", BuildPrompt(request))
        };

        if (!string.IsNullOrWhiteSpace(previousAnswer))
        {
            messages.Add(new OpenRouterMessage(
                "assistant",
                previousAnswer.Trim()));
            messages.Add(new OpenRouterMessage(
                "user",
                "Tiep tuc phan con lai tu ngay sau cau cuoi hien tai. " +
                "Khong lap lai noi dung da tra loi. Giu nguyen tieng Viet va Markdown."));
        }

        return new OpenRouterChatRequest(
            settings.Model,
            messages,
            settings.Temperature,
            settings.MaxOutputTokens,
            true);
    }

    private async Task<StreamedAnswerResult> ReadStreamedAnswerAsync(
        HttpResponseMessage response,
        Func<string, Task>? onDelta,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var finishReason = string.Empty;
        var sawDone = false;
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload.Equals("[DONE]", StringComparison.Ordinal))
            {
                sawDone = true;
                break;
            }

            var chunk = JsonSerializer.Deserialize<OpenRouterStreamResponse>(payload);
            var delta = chunk?.Choices?
                .FirstOrDefault()?
                .Delta?
                .Content;
            var chunkFinishReason = chunk?.Choices?
                .FirstOrDefault()?
                .FinishReason;

            if (!string.IsNullOrWhiteSpace(chunkFinishReason))
            {
                finishReason = chunkFinishReason;
            }

            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            builder.Append(delta);
            if (onDelta is not null)
            {
                await onDelta(delta);
            }
        }

        return new StreamedAnswerResult(builder.ToString(), finishReason, sawDone);
    }

    private static bool ShouldContinue(StreamedAnswerResult result, int attempt)
    {
        if (attempt >= MaxTotalAttempts)
        {
            return false;
        }

        return string.Equals(result.FinishReason, "length", StringComparison.Ordinal)
            || !result.SawDone;
    }

    private string BuildPrompt(AnswerGenerationRequest request)
    {
        var maxContextCharacters = Math.Max(1000, chatOptions.Value.MaxContextCharacters);
        var builder = new StringBuilder();
        builder.AppendLine("QUESTION:");
        builder.AppendLine(request.Question.Trim());
        builder.AppendLine();
        builder.AppendLine("CONTEXT:");

        var remaining = maxContextCharacters;
        foreach (var context in request.Contexts)
        {
            if (remaining <= 0)
            {
                break;
            }

            var header = $"[Nguon: {context.DocumentTitle}; Chuong: {context.ChapterTitle}; Chunk: {context.ChunkIndex}]";
            var content = context.Content.Length <= remaining
                ? context.Content
                : context.Content[..remaining];
            builder.AppendLine(header);
            builder.AppendLine(content);
            builder.AppendLine();
            remaining -= content.Length;
        }

        builder.AppendLine("RESPONSE REQUIREMENTS:");
        builder.AppendLine("- Tra loi truc tiep trong 1-2 cau dau, sau do moi giai thich chi tiet hon.");
        builder.AppendLine("- Tra loi bang Markdown ro rang, co tieu de va danh sach bullet neu phu hop.");
        builder.AppendLine("- Khi context du, huong den 5-7 doan hoac 6-10 bullet de cau tra loi day du hon.");
        builder.AppendLine("- Gop cac y quan trong tu CONTEXT vao cau tra loi chinh va co them giai thich hoac vi du ngan.");
        builder.AppendLine("- Co muc \"Y chinh tu tai lieu tham khao\" voi 3-6 bullet neu context co du du lieu.");
        builder.AppendLine("- Neu du lieu chua du, noi ro diem thieu thong tin va de xuat can bo sung gi.");
        builder.AppendLine("- Khong noi \"xem citation\" hoac yeu cau sinh vien bam nguon de hieu y chinh.");

        return builder.ToString();
    }

    private sealed record OpenRouterChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenRouterMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenRouterStreamResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenRouterStreamChoice>? Choices);

    private sealed record OpenRouterStreamChoice(
        [property: JsonPropertyName("delta")] OpenRouterStreamDelta? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record OpenRouterStreamDelta(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record StreamedAnswerResult(
        string Answer,
        string FinishReason,
        bool SawDone);
}
