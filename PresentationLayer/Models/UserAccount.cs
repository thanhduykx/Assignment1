namespace PresentationLayer.Models;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Provider { get; set; } = "Local";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
