using System.Net;
using System.Text;
using ServicesLayer;

namespace ServicesLayer.Tests;

public sealed class GeminiSemanticTextChunkerTests
{
    [Fact]
    public async Task CreateChunkingResultAsync_UsesGeminiSections_WhenValid()
    {
        var chunker = CreateChunker("""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"sections\":[{\"title\":\"Overview\",\"paragraphIds\":[1,2]},{\"title\":\"Assessment\",\"paragraphIds\":[3,4]}]}"
                      }
                    ]
                  }
                }
              ]
            }
            """);

        var result = await chunker.CreateChunkingResultAsync("""
            Course overview
            Students learn retrieval augmented generation with source citations.
            Assessment
            The final question asks students to explain indexed chunks.
            """);

        Assert.Equal("gemini-assisted-950-0", result.StrategyName);
        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("Overview", result.Chunks[0].SectionTitle);
        Assert.Equal("Assessment", result.Chunks[1].SectionTitle);
        Assert.Contains("Course overview", result.Chunks[0].Text);
        Assert.Contains("final question", result.Chunks[1].Text);
    }

    [Fact]
    public async Task CreateChunkingResultAsync_FallsBack_WhenGeminiReturnsInvalidSections()
    {
        var chunker = CreateChunker("""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"sections\":[{\"title\":\"Broken\",\"paragraphIds\":[2]}]}"
                      }
                    ]
                  }
                }
              ]
            }
            """);

        var result = await chunker.CreateChunkingResultAsync("""
            Lesson 1:
            The first paragraph must not be skipped.
            Lesson 2:
            The second paragraph is still indexed by fallback.
            """);

        Assert.Equal("paragraph-aware-950-0", result.StrategyName);
        Assert.NotEmpty(result.Chunks);
    }

    [Fact]
    public async Task CreateChunkingResultAsync_DoesNotOverlap_WhenGeminiSectionExceedsChunkSize()
    {
        var paragraphIds = string.Join(",", Enumerable.Range(1, 8));
        var chunker = CreateChunker($$"""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"sections\":[{\"title\":\"Student requirements\",\"paragraphIds\":[{{paragraphIds}}]}]}"
                      }
                    ]
                  }
                }
              ]
            }
            """);
        var text = string.Join("\n", Enumerable.Range(1, 8).Select(index =>
            $"- Requirement {index}: " + string.Join(' ', Enumerable.Repeat($"student must complete task {index}", 8))));

        var result = await chunker.CreateChunkingResultAsync(text);

        Assert.Equal("gemini-assisted-950-0", result.StrategyName);
        Assert.True(result.Chunks.Count > 1);
        for (var index = 1; index < result.Chunks.Count; index++)
        {
            var previousLines = result.Chunks[index - 1].Text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);
            var currentLines = result.Chunks[index].Text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);

            Assert.Empty(previousLines.Intersect(currentLines));
            Assert.True(result.Chunks[index].CharStart >= result.Chunks[index - 1].CharEnd);
        }
    }

    private static GeminiSemanticTextChunker CreateChunker(string responseJson)
    {
        var client = new HttpClient(new StaticJsonHandler(responseJson))
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        return new GeminiSemanticTextChunker(
            client,
            new GeminiSemanticChunkingOptions(
                "test-key",
                "gemini-test",
                true,
                16000,
                180),
            new ParagraphAwareTextChunker());
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StaticJsonHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
