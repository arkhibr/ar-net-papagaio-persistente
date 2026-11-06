using Chamados.Contracts;
using SharedKernel;
using SharedKernel.Domain;

namespace Chamados.Domain;

/// <summary>
/// Eventos de domínio auditáveis do Chamado (arquitetura/21-auditoria.md). O agregado descreve
/// o que mudou; quem fez e quando o registro é gravado são resolvidos na materialização
/// (ChamadosUnitOfWork). Só as operações do subconjunto auditado da especificação
/// (abertura, atribuição, resolução, escalonamento) levantam evento.
/// </summary>
internal abstract record EventoAuditavelDoChamado(Guid ChamadoId) : IDomainEvent, IAuditable
{
    public abstract string Acao { get; }
    public string TipoRecurso => "Chamado";
    public Guid RecursoId => ChamadoId;
    public virtual string? ValorAnterior => null;
    public abstract string? ValorNovo { get; }
    public virtual string? Motivo => null;
}

internal sealed record ChamadoAberto(Guid ChamadoId, PrioridadeChamado Prioridade, DateTimeOffset PrazoSla)
    : EventoAuditavelDoChamado(ChamadoId)
{
    public override string Acao => "ChamadoAberto";
    public override string? ValorNovo => $"Status={StatusChamado.Aberto};Prioridade={Prioridade};PrazoSla={PrazoSla:O}";
}

internal sealed record ChamadoAtribuido(Guid ChamadoId, Guid TecnicoId) : EventoAuditavelDoChamado(ChamadoId)
{
    public override string Acao => "ChamadoAtribuido";
    public override string? ValorAnterior => $"Status={StatusChamado.Aberto}";
    public override string? ValorNovo => $"Status={StatusChamado.EmAtendimento};TecnicoAtribuidoId={TecnicoId}";
}

internal sealed record ChamadoResolvido(Guid ChamadoId, string NotaResolucao) : EventoAuditavelDoChamado(ChamadoId)
{
    public override string Acao => "ChamadoResolvido";
    public override string? ValorAnterior => $"Status={StatusChamado.EmAtendimento}";
    public override string? ValorNovo => $"Status={StatusChamado.Resolvido}";
    public override string? Motivo => NotaResolucao;
}

internal sealed record ChamadoEscalonado(Guid ChamadoId, DateTimeOffset? EscalonadoAntesEm, DateTimeOffset EscalonadoEm)
    : EventoAuditavelDoChamado(ChamadoId)
{
    public override string Acao => "ChamadoEscalonado";
    public override string? ValorAnterior => EscalonadoAntesEm is null ? "Escalonado=False" : $"Escalonado=True;DataEscalonamento={EscalonadoAntesEm:O}";
    public override string? ValorNovo => $"Escalonado=True;DataEscalonamento={EscalonadoEm:O}";
}
