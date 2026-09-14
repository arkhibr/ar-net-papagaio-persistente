namespace Chamados.Infrastructure;

/// <summary>
/// Entidade de infraestrutura pura (tabela técnica) para IIdempotencyStore
/// (SharedKernel.Messaging), mesmo DbContext/transação do UnitOfWork (ver XML doc da
/// interface: "mesmo DbContext scoped do UnitOfWork", arquitetura/25-transacao-e-unit-of-work.md).
///
/// Decisão de escopo registrada aqui: esta tabela mora em Chamados.Infrastructure, não em algo
/// compartilhado entre módulos, porque só Chamados tem Commands hoje (Catalogo só tem Queries,
/// que nunca passam por IdempotencyBehavior). Cada módulo que ganhar Commands no futuro
/// provavelmente vai precisar da própria tabela de idempotência (ex.: "IdempotencyRecords" em
/// Catalogo.Infrastructure, se um dia Catalogo ganhar Commands) — nunca uma tabela
/// compartilhada entre módulos fisicamente, porque isso violaria a fronteira de dado por
/// módulo (arquitetura/04-comunicacao-entre-modulos.md) mesmo sendo uma tabela "técnica"
/// (arquitetura/22-migracao-de-schema.md é explícito: tabela técnica segue a mesma regra de
/// fronteira, sem exceção por ser "técnica").
///
/// IsCompleted + SerializedResponse: ver SharedKernel.Messaging.IdempotencyRecord. Índice
/// único em IdempotencyKey é o que garante a reserva atômica (arquitetura/25, "Idempotência
/// participa da mesma transação"): duas requisições concorrentes com a mesma chave nunca
/// conseguem inserir duas linhas "em andamento" - a segunda falha na inserção (violação de
/// unicidade), que ReserveAsync trata como sinal de corrida perdida.
/// </summary>
internal sealed class IdempotencyRecordEntity
{
    public Guid Id { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public string? SerializedResponse { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private IdempotencyRecordEntity()
    {
    }

    public static IdempotencyRecordEntity Reservar(string idempotencyKey, DateTimeOffset agora) => new()
    {
        Id = Guid.NewGuid(),
        IdempotencyKey = idempotencyKey,
        IsCompleted = false,
        SerializedResponse = null,
        CriadoEm = agora,
    };

    public void Concluir(string serializedResponse)
    {
        IsCompleted = true;
        SerializedResponse = serializedResponse;
    }
}
