namespace Chamados.Infrastructure;

/// <summary>
/// Registro de auditoria do módulo (schema mínimo de arquitetura/21-auditoria.md), gravado na
/// mesma transação da operação auditada. SourceIp fica nulo: ICurrentUser não expõe o IP e o
/// módulo não conhece HttpContext.
/// </summary>
internal sealed class RegistroAuditoriaEntity
{
    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid SessionId { get; private set; }
    public bool IsSystemActor { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string? Reason { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? SourceIp { get; private set; }

    private RegistroAuditoriaEntity()
    {
    }

    public static RegistroAuditoriaEntity De(
        SharedKernel.IAuditable evento, SharedKernel.ICurrentUser ator, DateTimeOffset agora, string? correlationId) => new()
    {
        Id = Guid.NewGuid(),
        OccurredAt = agora,
        ActorUserId = ator.UserId,
        SessionId = ator.SessionId,
        IsSystemActor = ator.IsSystemActor,
        Action = evento.Acao,
        ResourceType = evento.TipoRecurso,
        ResourceId = evento.RecursoId,
        OldValue = evento.ValorAnterior,
        NewValue = evento.ValorNovo,
        Reason = evento.Motivo,
        CorrelationId = correlationId,
    };
}
