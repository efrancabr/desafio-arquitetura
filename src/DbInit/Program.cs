using FluxoCaixa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Inicializador de banco: cria o schema no primeiro boot (EnsureCreated).
// Em produção real seria EF Migrations — ver ADR-007.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new InvalidOperationException("ConnectionString ausente.");

Console.WriteLine("Criando schema do banco...");
await using (var db = new FluxoCaixaDbContext(
    new DbContextOptionsBuilder<FluxoCaixaDbContext>()
        .UseNpgsql(connectionString)
        .Options))
{
    await db.Database.EnsureCreatedAsync();
}
Console.WriteLine("Schema criado com sucesso.");
