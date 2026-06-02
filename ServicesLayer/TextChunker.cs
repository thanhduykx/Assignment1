using System.Text;
using System.Text.RegularExpressions;

namespace ServicesLayer;

public sealed record TextChunk(
    int ChunkIndex,
    string Text,
    string SectionTitle,
    int CharStart,
    int CharEnd);

public interface ITextChunker
{
    string StrategyName { get; }
    IReadOnlyList<TextChunk> CreateChunks(string text);
}

public sealed class ParagraphAwareTextChunker : ITextChunker
{
    private const int TargetSize = 950;
    private const int MaxSize = 1200;
    private const int Overlap = 160;

    private static readonly Regex SpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NumberedHeadingRegex = new(@"^\d+(\.\d+)*[\).:-]?\s+\S+", RegexOptions.Compiled);
    private static readonly Regex NamedHeadingRegex = new(@"^(chapter|section|unit|lesson|week|module|part)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string StrategyName => $"paragraph-aware-{TargetSize}-{Overlap}";

    public IReadOnlyList<TextChunk> CreateChunks(string text)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<TextChunk>();
        }

        var blocks = CreateBlocks(normalized);
        if (blocks.Count == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        var builder = new StringBuilder();
        var chunkStart = 0;
        var chunkEnd = 0;
        var chunkSection = string.Empty;

        foreach (var block in blocks)
        {
            if (builder.Length == 0)
            {
                chunkStart = block.Start;
                chunkSection = block.SectionTitle;
            }

            var separatorLength = builder.Length == 0 ? 0 : Environment.NewLine.Length * 2;
            if (builder.Length > 0 && builder.Length + separatorLength + block.Text.Length > MaxSize)
            {
                AddChunk(chunks, builder.ToString(), chunkSection, chunkStart, chunkEnd);
                SeedOverlap(builder, chunkEnd, block.SectionTitle, out chunkStart, out chunkSection);
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(block.Text);
            chunkEnd = block.End;
            if (string.IsNullOrWhiteSpace(chunkSection))
            {
                chunkSection = block.SectionTitle;
            }
        }

        AddChunk(chunks, builder.ToString(), chunkSection, chunkStart, chunkEnd);
        return chunks;
    }

    private static void AddChunk(List<TextChunk> chunks, string text, string sectionTitle, int start, int end)
    {
        var chunkText = text.Trim();
        if (string.IsNullOrWhiteSpace(chunkText))
        {
            return;
        }

        chunks.Add(new TextChunk(
            chunks.Count + 1,
            chunkText,
            sectionTitle.Trim(),
            Math.Max(0, start),
            Math.Max(start, end)));
    }

    private static void SeedOverlap(StringBuilder builder, int previousEnd, string sectionTitle, out int chunkStart, out string chunkSection)
    {
        var previousText = builder.ToString();
        var overlapText = GetOverlapText(previousText);
        builder.Clear();

        if (string.IsNullOrWhiteSpace(overlapText))
        {
            chunkStart = previousEnd;
            chunkSection = sectionTitle;
            return;
        }

        builder.Append(overlapText);
        chunkStart = Math.Max(0, previousEnd - overlapText.Length);
        chunkSection = sectionTitle;
    }

    private static string GetOverlapText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= Overlap)
        {
            return trimmed;
        }

        var start = trimmed.Length - Overlap;
        var newline = trimmed.IndexOf('\n', start);
        if (newline >= 0 && trimmed.Length - newline >= 40)
        {
            return trimmed[newline..].Trim();
        }

        var space = trimmed.IndexOf(' ', start);
        if (space >= 0 && trimmed.Length - space >= 40)
        {
            return trimmed[space..].Trim();
        }

        return trimmed[start..].Trim();
    }

    private static IReadOnlyList<TextBlock> CreateBlocks(string normalized)
    {
        var blocks = new List<TextBlock>();
        var sectionTitle = string.Empty;
        var searchStart = 0;

        foreach (var line in normalized.Split('\n'))
        {
            var blockText = line.Trim();
            if (string.IsNullOrWhiteSpace(blockText))
            {
                searchStart += line.Length + 1;
                continue;
            }

            var start = normalized.IndexOf(blockText, searchStart, StringComparison.Ordinal);
            if (start < 0)
            {
                start = Math.Min(searchStart, normalized.Length);
            }

            var end = Math.Min(normalized.Length, start + blockText.Length);
            searchStart = Math.Min(normalized.Length, end + 1);

            if (IsHeading(blockText))
            {
                sectionTitle = blockText;
            }

            blocks.AddRange(SplitLongBlock(blockText, sectionTitle, start));
        }

        return blocks;
    }

    private static IEnumerable<TextBlock> SplitLongBlock(string text, string sectionTitle, int blockStart)
    {
        var offset = 0;
        while (offset < text.Length)
        {
            var remaining = text.Length - offset;
            var length = Math.Min(TargetSize, remaining);
            var end = offset + length;

            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end - 1, length);
                if (boundary > offset + TargetSize / 2)
                {
                    end = boundary;
                }
            }

            var slice = text[offset..end].Trim();
            if (!string.IsNullOrWhiteSpace(slice))
            {
                yield return new TextBlock(slice, sectionTitle, blockStart + offset, blockStart + end);
            }

            offset = end;
            while (offset < text.Length && char.IsWhiteSpace(text[offset]))
            {
                offset++;
            }
        }
    }

    private static bool IsHeading(string line)
    {
        if (line.Length > 120)
        {
            return false;
        }

        if (NamedHeadingRegex.IsMatch(line) || NumberedHeadingRegex.IsMatch(line) || line.EndsWith(':'))
        {
            return true;
        }

        var letters = line.Where(char.IsLetter).ToList();
        if (letters.Count < 4)
        {
            return false;
        }

        var upperRatio = letters.Count(char.IsUpper) / (double)letters.Count;
        return upperRatio >= 0.65;
    }

    private static string Normalize(string text)
    {
        var lines = (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => SpaceRegex.Replace(line.Trim(), " "));

        return string.Join('\n', lines).Trim();
    }

    private sealed record TextBlock(string Text, string SectionTitle, int Start, int End);
}
