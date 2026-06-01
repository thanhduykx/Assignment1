namespace ServicesLayer;

public sealed record HuggingFaceApiOptions(
    string ApiKey,
    string BaseAddress,
    bool Enabled);
