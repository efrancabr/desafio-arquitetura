using FluxoCaixa.Application;
using FluxoCaixa.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// A API de Consolidado é READ-ONLY: lê o read model gerado pelo Worker.
// Não registra handler de consolidação nem consumer — só o DbContext + store de leitura.

var connString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionString 'Postgres' ausente.");
builder.Services.AddDbContext<FluxoCaixa.Infrastructure.Data.FluxoCaixaDbContext>(o =>
    o.UseNpgsql(connString));
builder.Services.AddScoped<FluxoCaixa.Application.Consolidado.ISaldoDiarioStore,
    FluxoCaixa.Infrastructure.Repositories.SaldoDiarioStore>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidado API v1");
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
