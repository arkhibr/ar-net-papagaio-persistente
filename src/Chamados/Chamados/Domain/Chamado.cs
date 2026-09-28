using Chamados.Contracts;
using SharedKernel;
using SharedKernel.Domain;

namespace Chamados.Domain;

/// <summary>
/// Agregado rico do módulo Chamados. Construtor privado + factory Abrir(...).
/// Núcleo funcional puro: nunca lê relógio (TimeProvider é resolvido pela Application
/// e o DateTimeOffset chega aqui como parâmetro), nunca conhece ator/autorização
/// (isso é Application, ver plano-de-arquitetura.md secao 2 e arquitetura/02-dominio-hibrido.md).
/// internal: única superfície pública do módulo é Chamados.Contracts (arquitetura/01).
/// </summary>
internal sealed class Chamado : AggregateRoot
{
    public Guid Id { get; }
    public Guid SolicitanteId { get; }
    public Guid CategoriaId { get; }
    public Guid EquipeId { get; }
    public PrioridadeChamado Prioridade { get; private set; }
    public StatusChamado Status { get; private set; }
    public DateTimeOffset AbertoEm { get; }
    public DateTimeOffset PrazoSla { get; private set; }
    public Guid? TecnicoAtribuidoId { get; private set; }
    public string? NotaResolucao { get; private set; }
    public DateTimeOffset? ResolvidoEm { get; private set; }
    public DateTimeOffset? FechadoEm { get; private set; }
    public bool Escalonado { get; private set; }
    public DateTimeOffset? DataEscalonamento { get; private set; }

    private Chamado(
        Guid id,
        Guid solicitanteId,
        Guid categoriaId,
        Guid equipeId,
        PrioridadeChamado prioridade,
        DateTimeOffset abertoEm,
        DateTimeOffset prazoSla)
    {
        Id = id;
        SolicitanteId = solicitanteId;
        CategoriaId = categoriaId;
        EquipeId = equipeId;
        Prioridade = prioridade;
        Status = StatusChamado.Aberto;
        AbertoEm = abertoEm;
        PrazoSla = prazoSla;
        Escalonado = false;
        DataEscalonamento = null;
    }

    /// <summary>
    /// Abre um novo chamado. categoriaId/equipeId/prioridade/horasDeSla chegam como snapshot
    /// resolvido pela Application via Catalogo.Contracts (o agregado nunca consulta outro
    /// módulo nem recalcula o SLA por conta própria — Catalogo é o dono do dado).
    /// agora é o DateTimeOffset já resolvido pela Application a partir do TimeProvider.
    /// O prazo de SLA é "agora" somado a horasDeSla (horas corridas).
    /// </summary>
    public static Chamado Abrir(
        Guid solicitanteId,
        Guid categoriaId,
        Guid equipeId,
        PrioridadeChamado prioridade,
        int horasDeSla,
        DateTimeOffset agora)
    {
        var prazoSla = agora.AddHours(horasDeSla);

        var chamado = new Chamado(
            Guid.NewGuid(),
            solicitanteId,
            categoriaId,
            equipeId,
            prioridade,
            agora,
            prazoSla);

        chamado.Raise(new ChamadoAberto(chamado.Id, prioridade, prazoSla));

        return chamado;
    }

    /// <summary>Autoatribuição: técnico da equipe responsável pega o chamado da fila. Aberto -> EmAtendimento.</summary>
    public void Atribuir(Guid tecnicoId)
    {
        GarantirStatus(StatusChamado.Aberto, nameof(Atribuir));

        Status = StatusChamado.EmAtendimento;
        TecnicoAtribuidoId = tecnicoId;

        Raise(new ChamadoAtribuido(Id, tecnicoId));
    }

    /// <summary>
    /// Técnico atribuído devolve o chamado à fila. EmAtendimento -> Aberto, sem técnico: o
    /// vínculo do técnico que devolveu deixa de existir (A2 de achados.md).
    /// </summary>
    public void Devolver()
    {
        GarantirStatus(StatusChamado.EmAtendimento, nameof(Devolver));

        Status = StatusChamado.Aberto;
        TecnicoAtribuidoId = null;
    }

    /// <summary>
    /// Reclassifica a prioridade e recomputa o prazo de SLA a partir da abertura original
    /// (AbertoEm). horasDeSla é o snapshot do SLA da nova prioridade, resolvido pela
    /// Application via Catalogo.Contracts (o agregado nunca sabe mapear prioridade -> horas;
    /// só aplica o valor recebido). Válido em Aberto ou EmAtendimento.
    /// </summary>
    public void Reclassificar(PrioridadeChamado novaPrioridade, int horasDeSla)
    {
        if (Status is not (StatusChamado.Aberto or StatusChamado.EmAtendimento))
        {
            throw new DomainException(
                $"Não é possível reclassificar um chamado no status {Status}.");
        }

        Prioridade = novaPrioridade;
        PrazoSla = AbertoEm.AddHours(horasDeSla);
    }

    /// <summary>Resolve o chamado. Só a partir de EmAtendimento; exige nota de resolução não vazia.</summary>
    public void Resolver(string notaResolucao, DateTimeOffset agora)
    {
        GarantirStatus(StatusChamado.EmAtendimento, nameof(Resolver));

        if (string.IsNullOrWhiteSpace(notaResolucao))
        {
            throw new DomainException("A nota de resolução é obrigatória para resolver um chamado.");
        }

        Status = StatusChamado.Resolvido;
        NotaResolucao = notaResolucao;
        ResolvidoEm = agora;

        Raise(new ChamadoResolvido(Id, notaResolucao));
    }

    /// <summary>Fecha o chamado. Só a partir de Resolvido.</summary>
    public void Fechar(DateTimeOffset agora)
    {
        GarantirStatus(StatusChamado.Resolvido, nameof(Fechar));

        Status = StatusChamado.Fechado;
        FechadoEm = agora;
    }

    /// <summary>
    /// Reabre o chamado. Só a partir de Fechado, e só dentro de 5 dias corridos do fechamento.
    /// O chamado volta para a fila da equipe sem técnico atribuído (A2 de achados.md).
    /// </summary>
    public void Reabrir(DateTimeOffset agora)
    {
        GarantirStatus(StatusChamado.Fechado, nameof(Reabrir));

        var prazoParaReabertura = FechadoEm!.Value.AddDays(5);
        if (agora > prazoParaReabertura)
        {
            throw new DomainException(
                "Não é possível reabrir um chamado após 5 dias corridos do fechamento.");
        }

        Status = StatusChamado.Aberto;
        TecnicoAtribuidoId = null;
    }

    /// <summary>Marca o chamado como escalonado (flag + data). Só válido em Aberto ou EmAtendimento.</summary>
    public void Escalonar(DateTimeOffset agora)
    {
        if (Status is not (StatusChamado.Aberto or StatusChamado.EmAtendimento))
        {
            throw new DomainException(
                $"Não é possível escalonar um chamado no status {Status}.");
        }

        var escalonadoAntesEm = DataEscalonamento;

        Escalonado = true;
        DataEscalonamento = agora;

        Raise(new ChamadoEscalonado(Id, escalonadoAntesEm, agora));
    }

    private void GarantirStatus(StatusChamado statusEsperado, string acao)
    {
        if (Status != statusEsperado)
        {
            throw new DomainException(
                $"Não é possível executar '{acao}' com o chamado no status {Status}.");
        }
    }
}
