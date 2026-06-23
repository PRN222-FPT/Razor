using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed partial class DocumentChunker(
    IOptions<DocumentProcessingOptions> options) : IDocumentChunker
{
    private static readonly string[] DefaultSeparators = ["\r\n", "\n\n", "\n", " ", string.Empty];

    public IReadOnlyList<DocumentChunkDraft> CreateChunks(ParsedDocumentContent content)
    {
        if (content.Pages.Count == 0)
        {
            return [];
        }

        var chunkSize = Math.Max(200, options.Value.ChunkSize);
        var chunkOverlap = Math.Clamp(options.Value.ChunkOverlap, 0, chunkSize / 2);
        var separators = options.Value.Separators is { Length: > 0 }
            ? options.Value.Separators
            : DefaultSeparators;
        var chunks = new List<DocumentChunkDraft>();

        foreach (var page in content.Pages.OrderBy(page => page.PageNumber))
        {
            var normalizedText = Normalize(page.Text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                continue;
            }

            var prefix = $"[Trang {page.PageNumber}] ";
            var bodyLimit = Math.Max(1, chunkSize - prefix.Length);
            var bodyOverlap = Math.Min(chunkOverlap, bodyLimit / 2);
            var pageChunks = SplitPage(normalizedText, bodyLimit, bodyOverlap, separators);

            foreach (var pageChunk in pageChunks)
            {
                chunks.Add(new DocumentChunkDraft(chunks.Count, prefix + pageChunk));
            }
        }

        return chunks;
    }

    private static IReadOnlyList<string> SplitPage(
        string text,
        int chunkSize,
        int chunkOverlap,
        IReadOnlyList<string> separators)
    {
        var splits = RecursiveSplit(text, separators, chunkSize);

        return MergeSplits(splits, chunkSize, chunkOverlap);
    }

    private static IReadOnlyList<string> RecursiveSplit(
        string text,
        IReadOnlyList<string> separators,
        int chunkSize)
    {
        if (text.Length <= chunkSize)
        {
            return [text];
        }

        var separatorIndex = FindSeparatorIndex(text, separators);
        var separator = separators[separatorIndex];
        if (separator.Length == 0)
        {
            return text.Select(character => character.ToString()).ToList();
        }

        var nextSeparators = separators.Skip(separatorIndex + 1).ToArray();
        if (nextSeparators.Length == 0)
        {
            nextSeparators = [string.Empty];
        }

        var splits = new List<string>();
        foreach (var split in SplitWithSeparator(text, separator))
        {
            if (split.Length <= chunkSize)
            {
                splits.Add(split);
                continue;
            }

            splits.AddRange(RecursiveSplit(split, nextSeparators, chunkSize));
        }

        return splits;
    }

    private static int FindSeparatorIndex(string text, IReadOnlyList<string> separators)
    {
        for (var index = 0; index < separators.Count; index++)
        {
            var separator = separators[index];
            if (separator.Length == 0 || text.Contains(separator, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return separators.Count - 1;
    }

    private static IEnumerable<string> SplitWithSeparator(string text, string separator)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(separator, start, StringComparison.Ordinal);
            if (index < 0)
            {
                yield return text[start..];
                yield break;
            }

            var end = index + separator.Length;
            yield return text[start..end];
            start = end;
        }
    }

    private static IReadOnlyList<string> MergeSplits(
        IReadOnlyList<string> splits,
        int chunkSize,
        int chunkOverlap)
    {
        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var split in splits)
        {
            if (currentLength + split.Length > chunkSize && current.Count > 0)
            {
                AddChunk(chunks, current);

                while (currentLength > chunkOverlap
                    || (currentLength + split.Length > chunkSize && currentLength > 0))
                {
                    currentLength -= current[0].Length;
                    current.RemoveAt(0);
                }
            }

            current.Add(split);
            currentLength += split.Length;
        }

        AddChunk(chunks, current);

        return chunks;
    }

    private static void AddChunk(ICollection<string> chunks, IReadOnlyList<string> splits)
    {
        var chunk = string.Concat(splits).Trim();
        if (!string.IsNullOrWhiteSpace(chunk))
        {
            chunks.Add(chunk);
        }
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return RepeatedHorizontalWhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex RepeatedHorizontalWhitespaceRegex();
}
