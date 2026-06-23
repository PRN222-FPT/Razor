using System.Text;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class DocumentParserTests
{
    [Fact]
    public async Task ParseAsync_PdfWithEmbeddedText_DoesNotRequireOcrAssets()
    {
        var parser = CreateParser(
            new DocumentProcessingOptions
            {
                EnablePdfOcr = true,
                MinimumEmbeddedTextCharacters = 1,
                TessDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            });
        var filePath = WriteTempPdf(CreateTextPdf("Hello OCR fallback"));

        try
        {
            var result = await parser.ParseAsync(filePath, "pdf");

            Assert.Contains("Hello", result.Text, StringComparison.OrdinalIgnoreCase);
            var page = Assert.Single(result.Pages);
            Assert.Equal(1, page.PageNumber);
            Assert.Contains("Hello", page.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseAsync_BlankPdfWithOcrDisabled_ReturnsEmptyText()
    {
        var parser = CreateParser(new DocumentProcessingOptions { EnablePdfOcr = false });
        var filePath = WriteTempPdf(CreateBlankPdf());

        try
        {
            var result = await parser.ParseAsync(filePath, "pdf");

            Assert.True(string.IsNullOrWhiteSpace(result.Text));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseAsync_BlankPdfWithOcrEnabledAndMissingTessData_ThrowsControlledError()
    {
        var parser = CreateParser(
            new DocumentProcessingOptions
            {
                EnablePdfOcr = true,
                MinimumEmbeddedTextCharacters = 100,
                TessDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            });
        var filePath = WriteTempPdf(CreateBlankPdf());

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => parser.ParseAsync(filePath, "pdf"));

            Assert.Contains("OCR language data path", exception.Message);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static DocumentParser CreateParser(DocumentProcessingOptions options)
    {
        return new DocumentParser(Options.Create(options));
    }

    private static string WriteTempPdf(byte[] content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(filePath, content);

        return filePath;
    }

    private static byte[] CreateBlankPdf()
    {
        return CreatePdf(
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>"
            ]);
    }

    private static byte[] CreateTextPdf(string text)
    {
        var escapedText = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var stream = $"BT /F1 24 Tf 100 700 Td ({escapedText}) Tj ET";

        return CreatePdf(
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream"
            ]);
    }

    private static byte[] CreatePdf(IReadOnlyList<string> objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            NewLine = "\n"
        };
        var offsets = new List<long> { 0 };

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{index + 1} 0 obj\n");
            writer.Write(objects[index]);
            writer.Write("\nendobj\n");
            writer.Flush();
        }

        var xrefOffset = stream.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write($"{offset:0000000000} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        writer.Write($"startxref\n{xrefOffset}\n%%EOF\n");
        writer.Flush();

        return stream.ToArray();
    }
}
