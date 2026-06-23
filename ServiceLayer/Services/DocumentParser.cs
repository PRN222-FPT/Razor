using System.Text;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig;
using Tesseract;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace ServiceLayer.Services;

public sealed class DocumentParser(
    IOptions<DocumentProcessingOptions> processingOptions) : IDocumentParser
{
    public Task<ParsedDocumentContent> ParseAsync(
        string filePath,
        string fileType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedFileType = fileType.Trim().TrimStart('.').ToLowerInvariant();
        var pages = normalizedFileType switch
        {
            "pdf" => ParsePdf(filePath, processingOptions.Value, cancellationToken),
            "docx" => ParseDocx(filePath, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported document type '{normalizedFileType}'.")
        };

        return Task.FromResult(new ParsedDocumentContent(pages));
    }

    private static IReadOnlyList<ParsedDocumentPage> ParsePdf(
        string filePath,
        DocumentProcessingOptions options,
        CancellationToken cancellationToken)
    {
        var embeddedPages = ParseEmbeddedPdfPages(filePath, cancellationToken);
        if (!options.EnablePdfOcr
            || CountTextCharacters(embeddedPages) >= options.MinimumEmbeddedTextCharacters)
        {
            return embeddedPages;
        }

        return ParsePdfWithOcr(filePath, options, cancellationToken);
    }

    private static IReadOnlyList<ParsedDocumentPage> ParseEmbeddedPdfPages(
        string filePath,
        CancellationToken cancellationToken)
    {
        var pages = new List<ParsedDocumentPage>();

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(new ParsedDocumentPage(page.Number, page.Text));
        }

        return pages;
    }

    private static IReadOnlyList<ParsedDocumentPage> ParsePdfWithOcr(
        string filePath,
        DocumentProcessingOptions options,
        CancellationToken cancellationToken)
    {
        EnsureTessDataAvailable(options);

        var pages = new List<ParsedDocumentPage>();
        using var engine = new TesseractEngine(
            options.TessDataPath,
            options.OcrLanguages,
            EngineMode.Default);
        using var reader = DocLib.Instance.GetDocReader(
            filePath,
            new PageDimensions(options.PdfOcrRenderDpi / 72d));

        var pageCount = Math.Min(reader.GetPageCount(), options.MaxOcrPages);
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var pageReader = reader.GetPageReader(pageIndex);
            var pngBytes = RenderPageToPng(pageReader);
            using var pix = Pix.LoadFromMemory(pngBytes);
            using var page = engine.Process(pix, PageSegMode.Auto);

            pages.Add(new ParsedDocumentPage(pageIndex + 1, page.GetText()));
        }

        return pages;
    }

    private static byte[] RenderPageToPng(Docnet.Core.Readers.IPageReader pageReader)
    {
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var imageBytes = pageReader.GetImage(new NaiveTransparencyRemover());

        using var image = Image.LoadPixelData<Bgra32>(imageBytes, width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        return stream.ToArray();
    }

    private static void EnsureTessDataAvailable(DocumentProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TessDataPath) || !Directory.Exists(options.TessDataPath))
        {
            throw new InvalidOperationException("OCR language data path is not configured or does not exist.");
        }

        var languages = options.OcrLanguages
            .Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var language in languages)
        {
            var trainedDataPath = Path.Combine(options.TessDataPath, $"{language}.traineddata");
            if (!File.Exists(trainedDataPath))
            {
                throw new InvalidOperationException($"OCR language data '{language}' is missing.");
            }
        }
    }

    private static int CountTextCharacters(IReadOnlyList<ParsedDocumentPage> pages)
    {
        return pages.Sum(page => page.Text.Count(char.IsLetterOrDigit));
    }

    private static IReadOnlyList<ParsedDocumentPage> ParseDocx(
        string filePath,
        CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        return [new ParsedDocumentPage(1, body.InnerText)];
    }
}
