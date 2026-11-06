namespace SharedKernel.Domain;

/// <summary>
/// Base mínima de agregado que acumula eventos de domínio. O agregado só registra o que
/// aconteceu; quem materializa efeitos (auditoria, arquitetura/21) é o IUnitOfWork do módulo,
/// antes do seu único SaveChanges (arquitetura/25).
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
