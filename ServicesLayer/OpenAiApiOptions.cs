namespace ServicesLayer;

public sealed record OpenAiApiOptions(
    string ApiKey,
    string BaseAddress,
    bool Enabled);
