using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;

namespace Tests.Unit;

public class LancamentoTests
{
    [Fact]
    public void Criar_com_dados_validos_gera_lancamento()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var l = Lancamento.Criar(
            comercianteId: Guid.NewGuid(),
            data: hoje,
            tipo: TipoLancamento.Credito,
            valor: 100.50m,
            descricao: "Venda",
            idempotencyKey: "chave-123");

        Assert.NotEqual(Guid.Empty, l.Id);
        Assert.Equal(100.50m, l.Valor);
        Assert.Equal(TipoLancamento.Credito, l.Tipo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_com_valor_invalido_lanca_DomainException(decimal valor)
    {
        var ex = Assert.Throws<DomainException>(() => Lancamento.Criar(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow),
            TipoLancamento.Debito, valor, null, "chave-123"));
        Assert.Contains("maior que zero", ex.Message);
    }

    [Fact]
    public void Criar_com_idempotencyKey_curta_lanca_DomainException()
    {
        var ex = Assert.Throws<DomainException>(() => Lancamento.Criar(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow),
            TipoLancamento.Debito, 10m, null, "curta"));
        Assert.Contains("IdempotencyKey", ex.Message);
    }

    [Fact]
    public void Criar_com_data_futura_lanca_DomainException()
    {
        var ex = Assert.Throws<DomainException>(() => Lancamento.Criar(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TipoLancamento.Debito, 10m, null, "chave-123"));
        Assert.Contains("futuro", ex.Message);
    }

    [Fact]
    public void EfeitoNoSaldo_credito_soma_e_debito_subtrai()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var credito = Lancamento.Criar(Guid.NewGuid(), hoje, TipoLancamento.Credito, 200m, null, "chave-123");
        var debito = Lancamento.Criar(Guid.NewGuid(), hoje, TipoLancamento.Debito, 50m, null, "chave-456");

        Assert.Equal(200m, credito.EfeitoNoSaldo);
        Assert.Equal(-50m, debito.EfeitoNoSaldo);
    }
}

public class SaldoDiarioTests
{
    [Fact]
    public void Aplicar_acumula_debitos_creditos_e_quantidade()
    {
        var s = SaldoDiario.Novo(Guid.NewGuid(), new DateOnly(2025, 6, 1));
        s.Aplicar(debito: 30m, credito: 0m);
        s.Aplicar(debito: 0m, credito: 120m);

        Assert.Equal(30m, s.TotalDebitos);
        Assert.Equal(120m, s.TotalCreditos);
        Assert.Equal(90m, s.Saldo);
        Assert.Equal(2, s.QuantidadeLancamentos);
    }
}
