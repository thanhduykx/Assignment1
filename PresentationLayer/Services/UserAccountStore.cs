using System.Security.Cryptography;
using System.Text.Json;
using PresentationLayer.Models;

namespace PresentationLayer.Services;

public interface IUserAccountStore
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateLocalAsync(string fullName, string email, string password, CancellationToken cancellationToken = default);
    Task<UserAccount> GetOrCreateExternalAsync(string fullName, string email, string provider, CancellationToken cancellationToken = default);
    bool VerifyPassword(UserAccount account, string password);
}

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

    public UserAccountStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
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
                Provider = "Local"
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
                return existing;
            }

            var user = new UserAccount
            {
                FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedEmail : fullName.Trim(),
                Email = normalizedEmail,
                Provider = provider
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
        if (!File.Exists(_path))
        {
            return new List<UserAccount>();
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<UserAccount>>(stream, JsonOptions, cancellationToken) ?? new List<UserAccount>();
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
}
