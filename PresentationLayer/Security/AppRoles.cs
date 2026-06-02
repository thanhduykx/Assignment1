namespace PresentationLayer.Security;

public static class AppRoles
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Student, Lecturer, Admin];

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Student;
        }

        return All.FirstOrDefault(item => item.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Student;
    }

    public static bool IsKnown(string? role)
    {
        return All.Any(item => item.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
