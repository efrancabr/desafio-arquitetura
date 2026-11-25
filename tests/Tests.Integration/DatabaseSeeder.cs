using FluxoCaixa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration;

public static class DatabaseSeeder
{
    public static async Task EnsureSchemaAsync(string connectionString)
    {
        await using var db = new FluxoCaixaDbContext(
            new DbContextOptionsBuilder<FluxoCaixaDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        await db.Database.EnsureCreatedAsync();
    }
}
