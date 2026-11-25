using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Tests.Integration;

/// <summary>
/// Teste de integração E2E: API Lançamentos -> Outbox -> RabbitMQ -> Worker -> Consolidado.
/// Usa Testcontainers (PostgreSQL e RabbitMQ reais em Docker).
/// Requer Docker rodando localmente.
/// </summary>
public sealed class FluxoCaixaIntegrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    private HttpClient? _lancamentosClient;
    private HttpClient? _consolidadoClient;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbit.StartAsync();

        // Credenciais explícitas do container de teste
        Environment.SetEnvironmentVariable("RabbitMQ__User", "testuser");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "testpass");

        await DatabaseSeeder.EnsureSchemaAsync(_postgres.GetConnectionString());

        _lancamentosClient = TestHttpClientHelper.CreateLancamentosClient(_postgres.GetConnectionString(), _rabbit);
        _consolidadoClient = TestHttpClientHelper.CreateConsolidadoClient(_postgres.GetConnectionString());
    }

    public Task DisposeAsync()
    {
        _lancamentosClient?.Dispose();
        _consolidadoClient?.Dispose();
        return Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Lancamento_flui_para_o_consolidado()
    {
        var comerciante = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Registra crédito e débito
        var respCredito = await _lancamentosClient!.PostAsJsonAsync("/api/lancamentos", new
        {
            ComercianteId = comerciante,
            Data = hoje,
            Tipo = "credito",
            Valor = 500.00m,
            Descricao = "Venda no cartão",
            IdempotencyKey = $"it-{comerciante}-c1"
        });
        Assert.True(respCredito.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Created, respCredito.StatusCode);

        var respDebito = await _lancamentosClient.PostAsJsonAsync("/api/lancamentos", new
        {
            ComercianteId = comerciante,
            Data = hoje,
            Tipo = "debito",
            Valor = 200.00m,
            Descricao = "Pagamento fornecedor",
            IdempotencyKey = $"it-{comerciante}-d1"
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, respDebito.StatusCode);

        // 2. Aguarda o fluxo assíncrono: outbox -> rabbit -> worker -> read model
        var (consolidadoOk, saldo) = await WaitUntilAsync(async () =>
        {
            var resp = await _consolidadoClient!.GetFromJsonAsync<ConsolidadoResponse>(
                $"/api/consolidado/{comerciante}?inicio={hoje:yyyy-MM-dd}&fim={hoje:yyyy-MM-dd}");
            if (resp is { Dias.Count: 1 } and { SaldoTotalPeriodo: 300m }) return (true, resp.SaldoTotalPeriodo);
            return (false, resp?.SaldoTotalPeriodo ?? 0m);
        }, timeout: TimeSpan.FromSeconds(60));

        Assert.True(consolidadoOk, $"Consolidado não convergiu (último saldo: {saldo}).");
    }

    private static async Task<(bool, decimal)> WaitUntilAsync(Func<Task<(bool, decimal)>> condicao, TimeSpan timeout)
    {
        var limite = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < limite)
        {
            var (ok, valor) = await condicao();
            if (ok) return (true, valor);
            await Task.Delay(500);
        }
        var (_, ultimo) = await condicao();
        return (false, ultimo);
    }

    public sealed record SaldoDia(DateOnly Data, decimal TotalCreditos, decimal TotalDebitos, decimal Saldo, int QuantidadeLancamentos);
    public sealed record ConsolidadoResponse(Guid ComercianteId, List<SaldoDia> Dias, decimal SaldoTotalPeriodo);
}
