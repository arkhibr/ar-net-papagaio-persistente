using Catalogo.Contracts;
using Chamados.Application;
using Mediator;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real de IEquipeMembershipChecker (Chamados.Application), despachando
/// EhMembroDaEquipeQuery via ISender contra Catalogo.Contracts — mecanismo sancionado de
/// leitura cross-módulo (nunca acesso direto ao schema de Catalogo, arquitetura/04-comunicacao
/// -entre-modulos.md e arquitetura/29-modelagem-e-dados-entre-modulos.md). Consultada pelo
/// guard explícito de ReclassificarChamadoCommandHandler.
/// </summary>
internal sealed class EquipeMembershipChecker : IEquipeMembershipChecker
{
    private readonly ISender _sender;

    public EquipeMembershipChecker(ISender sender)
    {
        _sender = sender;
    }

    public async Task<bool> EhMembroDaEquipeAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new EhMembroDaEquipeQuery(tecnicoId, equipeId), cancellationToken);

        // EhMembroDaEquipeQuery é sempre Result.Success (verdadeiro ou falso) — ver XML doc do
        // próprio handler em Catalogo. Não há caminho de Failure a tratar aqui.
        return resultado.Value;
    }
}
