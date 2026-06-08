using ServicesLayer;

namespace ServicesLayer.Tests;

public sealed class ParagraphAwareTextChunkerTests
{
    [Fact]
    public void CreateChunks_ReturnsEmpty_ForWhitespace()
    {
        var chunker = new ParagraphAwareTextChunker();

        var chunks = chunker.CreateChunks(" \r\n \n\t ");

        Assert.Empty(chunks);
    }

    [Fact]
    public void CreateChunks_GroupsParagraphsWithoutOversizedChunks()
    {
        var chunker = new ParagraphAwareTextChunker();
        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(index =>
            $"Section {index}\n" +
            string.Join(' ', Enumerable.Repeat($"This paragraph explains lecture concept {index} with enough detail for retrieval.", 8))));

        var chunks = chunker.CreateChunks(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk =>
        {
            Assert.False(string.IsNullOrWhiteSpace(chunk.Text));
            Assert.True(chunk.Text.Length <= 1200);
            Assert.True(chunk.CharEnd >= chunk.CharStart);
        });
        Assert.Equal(Enumerable.Range(1, chunks.Count), chunks.Select(chunk => chunk.ChunkIndex));
        Assert.Contains(chunks, chunk => !string.IsNullOrWhiteSpace(chunk.SectionTitle));
    }

    [Fact]
    public void CreateChunks_DoesNotOverlapBetweenLongChunks()
    {
        var chunker = new ParagraphAwareTextChunker();
        var text = string.Join("\n", Enumerable.Range(1, 22).Select(index =>
            $"Lesson {index}: " + string.Join(' ', Enumerable.Repeat($"important retrieval sentence {index}", 5))));

        var chunks = chunker.CreateChunks(text);

        Assert.True(chunks.Count > 1);
        for (var index = 1; index < chunks.Count; index++)
        {
            Assert.True(chunks[index].CharStart >= chunks[index - 1].CharEnd);
        }
    }

    [Fact]
    public void CreateChunks_DoesNotRepeatBulletsAcrossChunks()
    {
        var chunker = new ParagraphAwareTextChunker();
        var bullets = Enumerable.Range(1, 24)
            .Select(index => $"- Requirement {index}: " + string.Join(' ', Enumerable.Repeat($"student action {index}", 8)));
        var text = "===== SINH VIEN CAN LAM GI TRONG IOT102 =====\n\nSinh vien can:\n\n" + string.Join("\n", bullets);

        var chunks = chunker.CreateChunks(text);

        Assert.True(chunks.Count > 1);
        for (var index = 1; index < chunks.Count; index++)
        {
            var previousLines = chunks[index - 1].Text.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("- Requirement"));
            var currentLines = chunks[index].Text.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("- Requirement"));

            Assert.Empty(previousLines.Intersect(currentLines));
        }
    }
}
