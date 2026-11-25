using FluxoCaixa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Data;

public sealed class FluxoCaixaDbContext : DbContext
{
    public FluxoCaixaDbContext(DbContextOptions<FluxoCaixaDbContext> options) : base(options) { }

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<SaldoDiario> SaldosDiarios => Set<SaldoDiario>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lancamento>(e =>
        {
            e.ToTable("lancamentos");
            e.HasKey(x => x.Id);
            e.Property(x => x.ComercianteId).IsRequired();
            e.Property(x => x.Data).IsRequired();
            e.Property(x => x.Tipo).HasConversion<int>().IsRequired();
            e.Property(x => x.Valor).HasPrecision(18, 2).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(200);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.CriadoEmUtc).IsRequired();
            e.HasIndex(x => new { x.ComercianteId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.ComercianteId, x.Data });
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.OcorridoEmUtc).IsRequired();
            e.HasIndex(x => new { x.Processado, x.OcorridoEmUtc });
        });

        modelBuilder.Entity<SaldoDiario>(e =>
        {
            e.ToTable("saldos_diarios");
            e.HasKey(x => new { x.ComercianteId, x.Data });
            e.Property(x => x.TotalDebitos).HasPrecision(18, 2);
            e.Property(x => x.TotalCreditos).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ProcessedMessage>(e =>
        {
            e.ToTable("processed_messages");
            e.HasKey(x => x.MessageId);
            e.Property(x => x.MessageId).HasMaxLength(128);
            e.Property(x => x.LancamentoId).IsRequired();
            e.Property(x => x.ProcessadoEmUtc).IsRequired();
        });
    }
}

/// <summary>Mensagem pendente de publicação (Transactional Outbox).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Tipo { get; set; }
    public required string Payload { get; set; }
    public DateTime OcorridoEmUtc { get; set; } = DateTime.UtcNow;
    public bool Processado { get; set; }
    public DateTime? PublicadoEmUtc { get; set; }
}

/// <summary>Registro de mensagens já consumidas (deduplicação no worker).</summary>
public sealed class ProcessedMessage
{
    public required string MessageId { get; set; }
    public Guid LancamentoId { get; set; }
    public DateTime ProcessadoEmUtc { get; set; } = DateTime.UtcNow;
}
