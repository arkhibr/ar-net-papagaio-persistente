namespace SharedKernel;

/// <summary>
/// Marca um evento de domínio que deve virar registro de auditoria (arquitetura/21-auditoria.md,
/// "Marcação do evento auditável"). O agregado descreve o que mudou, na linguagem do evento;
/// quem fez (ActorUserId/SessionId) é resolvido por ICurrentUser no momento de materializar o
/// registro, nunca carregado no evento. Materializado pelo IUnitOfWork do módulo, na mesma
/// transação da mudança de negócio.
/// </summary>
public interface IAuditable
{
    string Acao { get; }
    string TipoRecurso { get; }
    Guid RecursoId { get; }
    string? ValorAnterior { get; }
    string? ValorNovo { get; }
    string? Motivo { get; }
}
