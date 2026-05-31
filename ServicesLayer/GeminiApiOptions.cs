namespace ServicesLayer;

public sealed record GeminiApiOptions(
    string ApiKey,
    string ChatModel,
    string EmbeddingModel,
    int EmbeddingOutputDimensionality,
    bool Enabled);
