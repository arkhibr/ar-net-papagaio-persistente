using Chamados.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chamados.Infrastructure;

/// <summary>
/// DbContext do módulo Chamados. Só mapeia entidades do próprio módulo (Chamado, e a tabela
/// técnica de idempotência ChamadosIdempotencyRecord) — nunca um DbSet/IEntityTypeConfiguration
/// de outro módulo (Catalogo), regra sem exceção de arquitetura/04-comunicacao-entre-modulos.md
/// e arquitetura/01. internal: Chamados permanece 2 projetos, Infrastructure fundida no mesmo
/// assembly (arquitetura/01).
///
/// Nota de escopo (não implementado nesta rodada, persistencia-e-integracao): interceptor de
/// auditoria (IAuditable -> registro na mesma transação, arquitetura/21-auditoria.md) e
/// interceptor/coleta de eventos de domínio pós-commit (IDomainEventDispatcher,
/// arquitetura/25-transacao-e-unit-of-work.md) não foram pedidos nesta rodada e Chamado.cs
/// (Domain) ainda não acumula eventos de domínio — ficam como pendência registrada, não
/// inventados aqui.
/// </summary>
internal sealed class ChamadosDbContext : DbContext
{
    public ChamadosDbContext(DbContextOptions<ChamadosDbContext> options) : base(options)
    {
    }

    public DbSet<Chamado> Chamados => Set<Chamado>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChamadosDbContext).Assembly);
    }

    /// <summary>
    /// Achado real de smoke test manual (testes-manuais.md): o provider Sqlite lança
    /// NotSupportedException em ORDER BY sobre coluna DateTimeOffset ("SQLite does not support
    /// expressions of type 'DateTimeOffset' in ORDER BY clauses") — bloqueava
    /// ChamadoRepository.ListarPorSolicitanteAsync/ListarPorEquipeAsync (OrderByDescending
    /// AbertoEm), acionado via GET /api/v1/chamados/meus.
    ///
    /// Convenção a nível de DbContext (em vez de HasConversion por propriedade em
    /// ChamadoConfiguration/IdempotencyRecordConfiguration) para cobrir toda propriedade
    /// DateTimeOffset do módulo de uma vez, incluindo qualquer uma adicionada depois — evita
    /// reintroduzir o mesmo bug numa propriedade nova sem ninguém lembrar de repetir o
    /// HasConversion manualmente.
    ///
    /// Converte para DateTime (Kind=Utc), não para long/ticks: todo DateTimeOffset desta
    /// solution nasce de TimeProvider.GetUtcNow() (arquitetura/02-dominio-hibrido.md, "Domain
    /// nunca lê relógio"), sempre com offset zero — a conversão é sem perda e a ordenação por
    /// DateTime resultante é idêntica à ordenação cronológica original. Se um dia um
    /// DateTimeOffset com offset não-zero precisar ser persistido aqui, esta conversão passaria
    /// a perder o offset (mantém o instante UTC correto, mas não o offset local original) — não
    /// é o caso hoje, registrado para quem tocar nisso depois.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUtcDateTimeConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToUtcDateTimeConverter>();
    }

    private sealed class DateTimeOffsetToUtcDateTimeConverter()
        : ValueConverter<DateTimeOffset, DateTime>(
            v => v.UtcDateTime,
            v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

    private sealed class NullableDateTimeOffsetToUtcDateTimeConverter()
        : ValueConverter<DateTimeOffset?, DateTime?>(
            v => v.HasValue ? v.Value.UtcDateTime : null,
            v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : null);
}
