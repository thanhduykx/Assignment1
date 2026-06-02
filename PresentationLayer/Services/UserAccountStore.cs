using System.Security.Cryptography;
using System.Text.Json;
using PresentationLayer.Models;
using PresentationLayer.Security;

namespace PresentationLayer.Services;

public interface IUserAccountStore
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> GetByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateLocalAsync(string fullName, string email, string password, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateLocalForAdminAsync(string fullName, string email, string password, string role, CancellationToken cancellationToken = default);
    Task<UserAccount> GetOrCreateExternalAsync(string fullName, string email, string provider, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<UserAccount> GrantSubjectAccessAsync(Guid userId, Guid subjectId, CancellationToken cancellationToken = default);
    Task<UserAccount> RevokeSubjectAccessAsync(Guid userId, Guid subjectId, CancellationToken cancellationToken = default);
    bool VerifyPassword(UserAccount account, string password);
}

public sealed record SeedAdminOptions(bool Enabled, string FullName, string Email, string Password);

public sealed class UserAccountStore : IUserAccountStore
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly SeedAdminOptions _seedAdmin;

    public UserAccountStore(string path, SeedAdminOptions? seedAdmin = null)
    {
        _path = path;
        _seedAdmin = NormalizeSeedAdminOptions(seedAdmin);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken))
                .OrderBy(user => user.Email)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<UserAccount>> GetByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = AppRoles.Normalize(role);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken))
                .Where(user => user.Role == normalizedRole)
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Count > 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            return users.FirstOrDefault(user => string.Equals(user.Email, NormalizeEmail(email), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> CreateLocalAsync(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            if (users.Any(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This email is already registered.");
            }

            var user = new UserAccount
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = HashPassword(password),
                Provider = "Local",
                Role = IsSeedAdminEmail(normalizedEmail) || users.Count == 0 ? AppRoles.Admin : AppRoles.Student
            };

            users.Add(user);
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> CreateLocalForAdminAsync(
        string fullName,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!AppRoles.IsKnown(role))
        {
            throw new InvalidOperationException("Role is invalid.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            if (users.Any(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This email is already registered.");
            }

            var user = new UserAccount
            {
                FullName = NormalizeFullName(fullName),
                Email = normalizedEmail,
                PasswordHash = HashPassword(password),
                Provider = "Local",
                Role = IsSeedAdminEmail(normalizedEmail) ? AppRoles.Admin : AppRoles.Normalize(role)
            };

            users.Add(user);
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> GetOrCreateExternalAsync(string fullName, string email, string provider, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            var existing = users.FirstOrDefault(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var changed = EnsureSeedAdminRole(existing);
                if (TrackExternalProvider(existing, provider))
                {
                    changed = true;
                }

                if (ShouldUseExternalName(existing.FullName, fullName))
                {
                    existing.FullName = fullName.Trim();
                    changed = true;
                }

                if (changed)
                {
                    await SaveAsync(users, cancellationToken);
                }

                return existing;
            }

            var user = new UserAccount
            {
                FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedEmail : fullName.Trim(),
                Email = normalizedEmail,
                Provider = provider,
                Role = IsSeedAdminEmail(normalizedEmail) || users.Count == 0 ? AppRoles.Admin : AppRoles.Student
            };

            users.Add(user);
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!AppRoles.IsKnown(role))
        {
            throw new InvalidOperationException("Role is invalid.");
        }

        var normalizedRole = AppRoles.Normalize(role);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.Role == AppRoles.Admin
                && normalizedRole != AppRoles.Admin
                && users.Count(item => item.Role == AppRoles.Admin) <= 1)
            {
                throw new InvalidOperationException("Cannot demote the last admin.");
            }

            if (IsSeedAdminEmail(user.Email) && normalizedRole != AppRoles.Admin)
            {
                throw new InvalidOperationException("Cannot demote the seed admin.");
            }

            user.Role = normalizedRole;
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> GrantSubjectAccessAsync(Guid userId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.Role != AppRoles.Student)
            {
                throw new InvalidOperationException("Only students can be granted subject access.");
            }

            if (!user.AssignedSubjectIds.Contains(subjectId))
            {
                user.AssignedSubjectIds.Add(subjectId);
                user.AssignedSubjectIds = user.AssignedSubjectIds.Distinct().ToList();
                await SaveAsync(users, cancellationToken);
            }

            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> RevokeSubjectAccessAsync(Guid userId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            user.AssignedSubjectIds.RemoveAll(item => item == subjectId);
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            user.FullName = NormalizeFullName(fullName);
            await SaveAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool VerifyPassword(UserAccount account, string password)
    {
        if (string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            return false;
        }

        var parts = account.PasswordHash.Split('.', 3);
        if (parts.Length != 3 || parts[0] != "PBKDF2")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var actual = pbkdf2.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);
        return $"PBKDF2.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    private async Task<List<UserAccount>> LoadAsync(CancellationToken cancellationToken)
    {
        List<UserAccount> users;
        var changed = false;

        if (!File.Exists(_path))
        {
            users = new List<UserAccount>();
        }
        else
        {
            await using var stream = File.OpenRead(_path);
            users = await JsonSerializer.DeserializeAsync<List<UserAccount>>(stream, JsonOptions, cancellationToken) ?? new List<UserAccount>();
        }

        foreach (var user in users)
        {
            var normalizedRole = AppRoles.Normalize(user.Role);
            if (user.Role != normalizedRole)
            {
                user.Role = normalizedRole;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = user.Email;
                changed = true;
            }

            var distinctSubjectIds = user.AssignedSubjectIds.Distinct().ToList();
            if (distinctSubjectIds.Count != user.AssignedSubjectIds.Count)
            {
                user.AssignedSubjectIds = distinctSubjectIds;
                changed = true;
            }
        }

        if (EnsureSeedAdmin(users))
        {
            changed = true;
        }

        if (changed)
        {
            await SaveAsync(users, cancellationToken);
        }

        return users;
    }

    private async Task SaveAsync(List<UserAccount> users, CancellationToken cancellationToken)
    {
        var tempPath = $"{_path}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, users, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _path, true);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        var normalized = fullName.Trim();
        return normalized.Length <= 120
            ? normalized
            : throw new InvalidOperationException("Full name must be 120 characters or fewer.");
    }

    private bool EnsureSeedAdmin(List<UserAccount> users)
    {
        if (!_seedAdmin.Enabled)
        {
            return false;
        }

        var seedEmail = _seedAdmin.Email;
        var existing = users.FirstOrDefault(user => string.Equals(user.Email, seedEmail, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var changed = false;
            if (string.IsNullOrWhiteSpace(existing.FullName))
            {
                existing.FullName = _seedAdmin.FullName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Provider))
            {
                existing.Provider = "Local";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                existing.PasswordHash = HashPassword(_seedAdmin.Password);
                changed = true;
            }

            if (existing.Role != AppRoles.Admin)
            {
                existing.Role = AppRoles.Admin;
                changed = true;
            }

            return changed;
        }

        if (users.Any(user => user.Role == AppRoles.Admin))
        {
            return false;
        }

        users.Add(new UserAccount
        {
            FullName = _seedAdmin.FullName,
            Email = seedEmail,
            PasswordHash = HashPassword(_seedAdmin.Password),
            Provider = "Local",
            Role = AppRoles.Admin
        });

        return true;
    }

    private bool IsSeedAdminEmail(string email)
    {
        return _seedAdmin.Enabled
               && string.Equals(NormalizeEmail(email), _seedAdmin.Email, StringComparison.OrdinalIgnoreCase);
    }

    private bool EnsureSeedAdminRole(UserAccount user)
    {
        if (!IsSeedAdminEmail(user.Email) || user.Role == AppRoles.Admin)
        {
            return false;
        }

        user.Role = AppRoles.Admin;
        return true;
    }

    private static bool TrackExternalProvider(UserAccount user, string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Provider))
        {
            user.Provider = provider.Trim();
            return true;
        }

        var providers = user.Provider
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (providers.Any(item => item.Equals(provider, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        providers.Add(provider.Trim());
        user.Provider = string.Join(", ", providers);
        return true;
    }

    private static bool ShouldUseExternalName(string currentName, string externalName)
    {
        return !string.IsNullOrWhiteSpace(externalName)
               && (string.IsNullOrWhiteSpace(currentName)
                   || currentName.Equals("System Admin", StringComparison.OrdinalIgnoreCase)
                   || currentName.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                   || currentName.Contains('@'));
    }

    private static SeedAdminOptions NormalizeSeedAdminOptions(SeedAdminOptions? options)
    {
        var fullName = string.IsNullOrWhiteSpace(options?.FullName) ? "System Admin" : options.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(options?.Email) ? "admin@eduvietrag.local" : NormalizeEmail(options.Email);
        var password = string.IsNullOrWhiteSpace(options?.Password) ? "Admin@12345" : options.Password;
        return new SeedAdminOptions(options?.Enabled ?? true, fullName, email, password);
    }
}
