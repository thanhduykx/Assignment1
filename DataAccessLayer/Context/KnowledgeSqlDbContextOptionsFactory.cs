using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Context;

internal static class KnowledgeSqlDbContextOptionsFactory
{
    public static DbContextOptions<KnowledgeSqlDbContext> Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is not configured.");
        }

        return new DbContextOptionsBuilder<KnowledgeSqlDbContext>()
            .UseSqlServer(connectionString)
            .Options;
    }
}
