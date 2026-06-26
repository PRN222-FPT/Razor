using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed partial class DocumentChunker(
    IOptions<DocumentProcessingOptions> options) : IDocumentChunker
{
    private static readonly string[] DefaultSeparators = ["\r\n", "\n\n", "\n", " ", string.Empty];

    public IReadOnlyList<DocumentChunkDraft> CreateChunks(
        ParsedDocumentContent content,
        DocumentChunkingSettings settings)
    {
        if (content.Pages.Count == 0)
        {
            return [];
        }

        return NormalizeStrategy(settings.Strategy) switch
        {
            DocumentChunkingStrategies.FixedSized => CreateFixedSizeChunks(content, settings),
            DocumentChunkingStrategies.Semantic => CreateSemanticChunks(content, settings),
            _ => CreateRecursiveChunks(content, settings)
        };
    }

    private IReadOnlyList<DocumentChunkDraft> CreateRecursiveChunks(
        ParsedDocumentContent content,
        DocumentChunkingSettings settings)
    {
        var chunkSize = Math.Max(1, settings.ChunkSize);
        var chunkOverlap = Math.Clamp(settings.ChunkOverlap, 0, chunkSize / 2);
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
            var pageChunks = SplitPageRecursive(normalizedText, bodyLimit, bodyOverlap, separators);

            foreach (var pageChunk in pageChunks)
            {
                chunks.Add(new DocumentChunkDraft(chunks.Count, prefix + pageChunk));
            }
        }

        return chunks;
    }

    private static IReadOnlyList<DocumentChunkDraft> CreateFixedSizeChunks(
        ParsedDocumentContent content,
        DocumentChunkingSettings settings)
    {
        var chunkSize = Math.Max(1, settings.ChunkSize);
        var chunkOverlap = Math.Clamp(settings.ChunkOverlap, 0, chunkSize / 2);
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
            var pageChunks = SplitPageFixed(normalizedText, bodyLimit, chunkOverlap);

            foreach (var pageChunk in pageChunks)
            {
                chunks.Add(new DocumentChunkDraft(chunks.Count, prefix + pageChunk));
            }
        }

        return chunks;
    }

    private static IReadOnlyList<DocumentChunkDraft> CreateSemanticChunks(
        ParsedDocumentContent content,
        DocumentChunkingSettings settings)
    {
        var chunkSize = Math.Max(1, settings.ChunkSize);
        var chunks = new List<DocumentChunkDraft>();

        foreach (var page in content.Pages.OrderBy(page => page.PageNumber))
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            var prefix = $"[Trang {page.PageNumber}] ";
            var bodyLimit = Math.Max(1, chunkSize - prefix.Length);
            var units = BuildSemanticUnits(page.Text, bodyLimit);
            var pageChunks = MergeSemanticUnits(units, bodyLimit);

            foreach (var pageChunk in pageChunks)
            {
                chunks.Add(new DocumentChunkDraft(chunks.Count, prefix + pageChunk));
            }
        }

        return chunks;
    }

    private static IReadOnlyList<string> SplitPageRecursive(
        string text,
        int chunkSize,
        int chunkOverlap,
        IReadOnlyList<string> separators)
    {
        var splits = RecursiveSplit(text, separators, chunkSize);

        return MergeSplits(splits, chunkSize, chunkOverlap);
    }

    private static IReadOnlyList<string> SplitPageFixed(string text, int chunkSize, int chunkOverlap)
    {
        if (text.Length <= chunkSize)
        {
            return [text];
        }

        var chunks = new List<string>();
        var nextStart = 0;

        while (nextStart < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - nextStart);
            var chunk = text.Substring(nextStart, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (nextStart + length >= text.Length)
            {
                break;
            }

            nextStart += Math.Max(1, chunkSize - chunkOverlap);
        }

        return chunks;
    }

    private static IReadOnlyList<string> BuildSemanticUnits(string text, int chunkSize)
    {
        var paragraphs = ParagraphBoundaryRegex()
            .Split(text)
            .Select(Normalize)
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToList();

        if (paragraphs.Count == 0)
        {
            return [];
        }

        var units = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length <= chunkSize)
            {
                units.Add(paragraph);
                continue;
            }

            foreach (var sentence in SentenceBoundaryRegex()
                         .Split(paragraph)
                         .Select(Normalize)
                         .Where(sentence => !string.IsNullOrWhiteSpace(sentence)))
            {
                if (sentence.Length <= chunkSize)
                {
                    units.Add(sentence);
                    continue;
                }

                units.AddRange(SliceLongText(sentence, chunkSize));
            }
        }

        return units;
    }

    private static IReadOnlyList<string> MergeSemanticUnits(IReadOnlyList<string> units, int chunkSize)
    {
        if (units.Count == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var unit in units)
        {
            var separatorLength = current.Count == 0 ? 0 : Environment.NewLine.Length * 2;
            if (current.Count > 0 && currentLength + separatorLength + unit.Length > chunkSize)
            {
                AddSemanticChunk(chunks, current);
                current.Clear();
                currentLength = 0;
                separatorLength = 0;
            }

            current.Add(unit);
            currentLength += separatorLength + unit.Length;
        }

        AddSemanticChunk(chunks, current);

        return chunks;
    }

    private static IReadOnlyList<string> SliceLongText(string text, int chunkSize)
    {
        var slices = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            var chunk = text.Substring(start, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                slices.Add(chunk);
            }

            start += length;
        }

        return slices;
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

    private static void AddSemanticChunk(ICollection<string> chunks, IReadOnlyList<string> units)
    {
        if (units.Count == 0)
        {
            return;
        }

        var chunk = string.Join(Environment.NewLine + Environment.NewLine, units).Trim();
        if (!string.IsNullOrWhiteSpace(chunk))
        {
            chunks.Add(chunk);
        }
    }

    private static string NormalizeStrategy(string? strategy)
    {
        return strategy?.Trim().ToLowerInvariant() switch
        {
            DocumentChunkingStrategies.Semantic => DocumentChunkingStrategies.Semantic,
            DocumentChunkingStrategies.FixedSized => DocumentChunkingStrategies.FixedSized,
            _ => DocumentChunkingStrategies.Recursive
        };
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return RepeatedHorizontalWhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"(?:\r?\n){2,}")]
    private static partial Regex ParagraphBoundaryRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex RepeatedHorizontalWhitespaceRegex();
}
