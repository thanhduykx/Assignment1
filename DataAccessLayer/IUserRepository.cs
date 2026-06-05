using DataAccessLayer.Entities;

namespace DataAccessLayer;

public interface IUserRepository
{
    Task<IReadOnlyList<KnowledgeSqlUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeSqlUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlUser>> GetByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlUser> CreateAsync(string fullName, string email, string passwordHash, string role, string provider, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlUser> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlUser> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasAnyAsync(CancellationToken cancellationToken = default);
    bool VerifyPassword(KnowledgeSqlUser user, string password);
}