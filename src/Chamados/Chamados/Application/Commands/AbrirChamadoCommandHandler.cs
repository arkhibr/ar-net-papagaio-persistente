using Catalogo.Contracts;
using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>
/// Consulta Catalogo.Contracts (ISender) para resolver EquipeId/HorasDeSla por
/// categoria+prioridade e congela o snapshot em Chamado.Abrir(...) (plano-de-arquitetura.md
/// secao 2/3). A prioridade usada na consulta é a escolhida pelo solicitante no Command
/// (request.Prioridade), traduzida para Catalogo.Contracts.PrioridadeServico — nunca um valor
/// fixo hardcoded (correção registrada em especificacao-clarificada.md, "Quem define prioridade").
/// internal: descoberto por DI dentro do próprio assembly, nunca instanciado de fora (arquitetura/01).
/// </summary>
internal sealed class AbrirChamadoCommandHandler : IRequestHandler<AbrirChamadoCommand, Result<Guid>>
{
    private readonly IChamadoRepository _repository;
    private readonly ISender _sender;
    private readonly TimeProvider _timeProvider;

    public AbrirChamadoCommandHandler(IChamadoRepository repository, ISender sender, TimeProvider timeProvider)
    {
        _repository = repository;
        _sender = sender;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Guid>> Handle(AbrirChamadoCommand request, CancellationToken cancellationToken)
    {
        var prioridadeServico = (PrioridadeServico)request.Prioridade;

        var resolucao = await _sender.Send(
            new ResolverEquipeESlaQuery(request.CategoriaId, prioridadeServico), cancellationToken);

        if (resolucao.IsFailure)
        {
            return Result<Guid>.Failure(resolucao.Error!);
        }

        var agora = _timeProvider.GetUtcNow();

        var chamado = Chamado.Abrir(
            request.SolicitanteId,
            request.CategoriaId,
            resolucao.Value!.EquipeId,
            request.Prioridade,
            resolucao.Value!.HorasDeSla,
            agora);

        await _repository.AdicionarAsync(chamado, cancellationToken);

        return Result<Guid>.Success(chamado.Id);
    }
}
