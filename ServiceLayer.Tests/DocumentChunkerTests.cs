using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class DocumentChunkerTests
{
    [Fact]
    public void CreateChunks_ReturnsEmptyList_WhenTextIsBlank()
    {
        var chunker = CreateChunker();

        var chunks = chunker.CreateChunks(new ParsedDocumentContent(" \r\n \t "));

        Assert.Empty(chunks);
    }

    [Fact]
    public void CreateChunks_NormalizesWhitespaceAndPreservesOrder()
    {
        var chunker = CreateChunker(chunkSize: 80, chunkOverlap: 10);

        var chunks = chunker.CreateChunks(new ParsedDocumentContent("""
            First    sentence has extra spacing.


            Second sentence follows.
            Third sentence follows.
            """));

        Assert.NotEmpty(chunks);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.ChunkIndex));
        Assert.DoesNotContain("    ", string.Join(' ', chunks.Select(chunk => chunk.Content)));
        Assert.All(chunks, chunk => Assert.StartsWith("[Trang 1] ", chunk.Content));
    }

    [Fact]
    public void CreateChunks_UsesConfiguredOverlap_ForLongText()
    {
        var chunker = CreateChunker(chunkSize: 200, chunkOverlap: 20);
        var text = string.Concat(Enumerable.Repeat("abcdefghij", 45));

        var chunks = chunker.CreateChunks(new ParsedDocumentContent(text));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 200));
        Assert.Equal(
            chunks[0].Content[^20..],
            chunks[1].Content["[Trang 1] ".Length..("[Trang 1] ".Length + 20)]);
    }

    [Fact]
    public void CreateChunks_ChunksEachPageAndPreservesPagePrefix()
    {
        var chunker = CreateChunker(chunkSize: 70, chunkOverlap: 5);
        var content = new ParsedDocumentContent(
            [
                new ParsedDocumentPage(1, "Page one paragraph one.\n\nPage one paragraph two."),
                new ParsedDocumentPage(2, "Page two paragraph one.\n\nPage two paragraph two.")
            ]);

        var chunks = chunker.CreateChunks(content);

        Assert.Contains(chunks, chunk => chunk.Content.StartsWith("[Trang 1] ", StringComparison.Ordinal));
        Assert.Contains(chunks, chunk => chunk.Content.StartsWith("[Trang 2] ", StringComparison.Ordinal));
        Assert.DoesNotContain(chunks, chunk =>
            chunk.Content.Contains("[Trang 1]", StringComparison.Ordinal)
            && chunk.Content.Contains("[Trang 2]", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateChunks_UsesSeparatorPriorityBeforeCharacterFallback()
    {
        var chunker = CreateChunker(
            chunkSize: 210,
            chunkOverlap: 0,
            separators: ["\n\n", " ", string.Empty]);
        var firstParagraph = string.Join(' ', Enumerable.Repeat("alpha", 20));
        var secondParagraph = string.Join(' ', Enumerable.Repeat("delta", 20));
        var content = new ParsedDocumentContent($"{firstParagraph}\n\n{secondParagraph}");

        var chunks = chunker.CreateChunks(content);

        Assert.Equal(2, chunks.Count);
        Assert.EndsWith(firstParagraph, chunks[0].Content);
        Assert.EndsWith(secondParagraph, chunks[1].Content);
    }

    private static DocumentChunker CreateChunker(
        int chunkSize = 1400,
        int chunkOverlap = 180,
        string[]? separators = null)
    {
        return new DocumentChunker(
            Options.Create(
                new DocumentProcessingOptions
                {
                    ChunkSize = chunkSize,
                    ChunkOverlap = chunkOverlap,
                    Separators = separators ?? ["\r\n", "\n\n", "\n", " ", string.Empty]
                }));
    }
}
