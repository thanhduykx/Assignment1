namespace PresentationLayer.Models;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Provider { get; set; } = "Local";
    public string Role { get; set; } = PresentationLayer.Security.AppRoles.Student;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Guid> AssignedSubjectIds { get; set; } = new();
}
