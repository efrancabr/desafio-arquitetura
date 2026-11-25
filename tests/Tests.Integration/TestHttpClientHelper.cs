using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Tests.Integration;

public static class TestHttpClientHelper
{
    /// <summary>Cria cliente HTTP da API de Lançamentos apontando para os containers de teste.</summary>
    public static HttpClient CreateLancamentosClient(string pgConn, Testcontainers.RabbitMq.RabbitMqContainer rabbit)
    {
        var factory = new LancamentosAppFactory(pgConn, rabbit.Hostname, rabbit.GetMappedPublicPort(5672));
        return factory.CreateClient();
    }

    public static HttpClient CreateConsolidadoClient(string pgConn)
    {
        var factory = new ConsolidadoAppFactory(pgConn);
        return factory.CreateClient();
    }
}
