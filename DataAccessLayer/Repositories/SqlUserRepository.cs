using System.Security.Cryptography;
using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public sealed class SqlUserRepository : IUserRepository
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    private readonly KnowledgeSqlDbContext _context;

    public SqlUserRepository(KnowledgeSqlDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<KnowledgeSqlUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<KnowledgeSqlUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSqlUser>> GetByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = role.Trim().ToLowerInvariant();
        return await _context.Users
            .AsNoTracking()
            .Where(user => user.Role == normalizedRole)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlUser> CreateAsync(
        string fullName,
        string email,
        string passwordHash,
        string role,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var existing = await _context.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("This email is already registered.");
        }

        var user = new KnowledgeSqlUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = role.Trim().ToLowerInvariant(),
            Provider = string.IsNullOrWhiteSpace(provider) ? "local" : provider.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<KnowledgeSqlUser> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.FullName = fullName.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<KnowledgeSqlUser> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.Role = role.Trim().ToLowerInvariant();
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(cancellationToken);
    }

    public bool VerifyPassword(KnowledgeSqlUser user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var parts = user.PasswordHash.Split('.', 3);
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

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);
        return $"PBKDF2.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}