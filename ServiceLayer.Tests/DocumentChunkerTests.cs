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

        var chunks = chunker.CreateChunks(
            new ParsedDocumentContent(" \r\n \t "),
            RecursiveSettings());

        Assert.Empty(chunks);
    }

    [Fact]
    public void CreateChunks_Recursive_NormalizesWhitespaceAndPreservesOrder()
    {
        var chunker = CreateChunker();

        var chunks = chunker.CreateChunks(
            new ParsedDocumentContent("""
                First    sentence has extra spacing.


                Second sentence follows.
                Third sentence follows.
                """),
            new DocumentChunkingSettings(DocumentChunkingStrategies.Recursive, 80, 10));

        Assert.NotEmpty(chunks);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.ChunkIndex));
        Assert.DoesNotContain("    ", string.Join(' ', chunks.Select(chunk => chunk.Content)));
        Assert.All(chunks, chunk => Assert.StartsWith("[Trang 1] ", chunk.Content));
    }

    [Fact]
    public void CreateChunks_Recursive_UsesConfiguredOverlap_ForLongText()
    {
        var chunker = CreateChunker();
        var text = string.Concat(Enumerable.Repeat("abcdefghij", 45));

        var chunks = chunker.CreateChunks(
            new ParsedDocumentContent(text),
            new DocumentChunkingSettings(DocumentChunkingStrategies.Recursive, 200, 20));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 200));
        Assert.Equal(
            chunks[0].Content[^20..],
            chunks[1].Content["[Trang 1] ".Length..("[Trang 1] ".Length + 20)]);
    }

    [Fact]
    public void CreateChunks_Recursive_ChunksEachPageAndPreservesPagePrefix()
    {
        var chunker = CreateChunker();
        var content = new ParsedDocumentContent(
            [
                new ParsedDocumentPage(1, "Page one paragraph one.\n\nPage one paragraph two."),
                new ParsedDocumentPage(2, "Page two paragraph one.\n\nPage two paragraph two.")
            ]);

        var chunks = chunker.CreateChunks(
            content,
            new DocumentChunkingSettings(DocumentChunkingStrategies.Recursive, 70, 5));

        Assert.Contains(chunks, chunk => chunk.Content.StartsWith("[Trang 1] ", StringComparison.Ordinal));
        Assert.Contains(chunks, chunk => chunk.Content.StartsWith("[Trang 2] ", StringComparison.Ordinal));
        Assert.DoesNotContain(chunks, chunk =>
            chunk.Content.Contains("[Trang 1]", StringComparison.Ordinal)
            && chunk.Content.Contains("[Trang 2]", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateChunks_Recursive_UsesSeparatorPriorityBeforeCharacterFallback()
    {
        var chunker = CreateChunker(separators: ["\n\n", " ", string.Empty]);
        var firstParagraph = string.Join(' ', Enumerable.Repeat("alpha", 20));
        var secondParagraph = string.Join(' ', Enumerable.Repeat("delta", 20));
        var content = new ParsedDocumentContent($"{firstParagraph}\n\n{secondParagraph}");

        var chunks = chunker.CreateChunks(
            content,
            new DocumentChunkingSettings(DocumentChunkingStrategies.Recursive, 210, 0));

        Assert.Equal(2, chunks.Count);
        Assert.EndsWith(firstParagraph, chunks[0].Content);
        Assert.EndsWith(secondParagraph, chunks[1].Content);
    }

    [Fact]
    public void CreateChunks_FixedSized_UsesConfiguredWindowAndOverlap()
    {
        var chunker = CreateChunker();
        var text = string.Concat(Enumerable.Repeat("abcdefghij", 20));

        var chunks = chunker.CreateChunks(
            new ParsedDocumentContent(text),
            new DocumentChunkingSettings(DocumentChunkingStrategies.FixedSized, 60, 10));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.StartsWith("[Trang 1] ", chunk.Content));

        var firstBody = chunks[0].Content["[Trang 1] ".Length..];
        var secondBody = chunks[1].Content["[Trang 1] ".Length..];
        Assert.Equal(firstBody[^10..], secondBody[..10]);
    }

    [Fact]
    public void CreateChunks_Semantic_SplitsByParagraphAndSentence()
    {
        var chunker = CreateChunker();
        var content = new ParsedDocumentContent("""
            First paragraph has one clear idea. It stays together.

            Second paragraph starts another idea. It should become another semantic chunk.
            """);

        var chunks = chunker.CreateChunks(
            content,
            new DocumentChunkingSettings(DocumentChunkingStrategies.Semantic, 70, 0));

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, chunk => Assert.StartsWith("[Trang 1] ", chunk.Content));
        Assert.Contains("First paragraph", chunks[0].Content);
        Assert.Contains(chunks, chunk => chunk.Content.Contains("Second paragraph", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateChunks_Semantic_FallsBackForVeryLongSentence()
    {
        var chunker = CreateChunker();
        var content = new ParsedDocumentContent(string.Concat(Enumerable.Repeat("longtoken", 40)));

        var chunks = chunker.CreateChunks(
            content,
            new DocumentChunkingSettings(DocumentChunkingStrategies.Semantic, 50, 0));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 50 + "[Trang 1] ".Length));
    }

    private static DocumentChunkingSettings RecursiveSettings()
    {
        return new DocumentChunkingSettings(
            DocumentChunkingStrategies.Recursive,
            DocumentChunkingDefaults.RecursiveChunkSize,
            DocumentChunkingDefaults.RecursiveChunkOverlap);
    }

    private static DocumentChunker CreateChunker(string[]? separators = null)
    {
        return new DocumentChunker(
            Options.Create(
                new DocumentProcessingOptions
                {
                    ChunkSize = DocumentChunkingDefaults.RecursiveChunkSize,
                    ChunkOverlap = DocumentChunkingDefaults.RecursiveChunkOverlap,
                    Separators = separators ?? ["\r\n", "\n\n", "\n", " ", string.Empty]
                }));
    }
}
