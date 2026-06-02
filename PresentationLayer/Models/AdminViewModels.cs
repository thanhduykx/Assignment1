namespace PresentationLayer.Models;

public sealed class AdminUsersViewModel
{
    public IReadOnlyList<AdminUserRowViewModel> Users { get; set; } = Array.Empty<AdminUserRowViewModel>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public sealed class AdminUserRowViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsLastAdmin { get; set; }
}

public sealed class UpdateUserRoleViewModel
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}
