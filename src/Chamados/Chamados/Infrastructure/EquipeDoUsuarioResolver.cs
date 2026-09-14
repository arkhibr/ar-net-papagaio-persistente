using Catalogo.Contracts;
using Chamados.Application;
using Mediator;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real de IEquipeDoUsuarioResolver (Chamados.Application), despachando
/// ResolverEquipeDoTecnicoQuery via ISender contra Catalogo.Contracts — mesmo mecanismo
/// sancionado de leitura cross-módulo de EquipeMembershipChecker (arquitetura/04). Consultada
/// por FilaDaEquipeQueryHandler para resolver a equipe do técnico/supervisor autenticado.
/// </summary>
internal sealed class EquipeDoUsuarioResolver : IEquipeDoUsuarioResolver
{
    private readonly ISender _sender;

    public EquipeDoUsuarioResolver(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Guid?> ResolverEquipeIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new ResolverEquipeDoTecnicoQuery(userId), cancellationToken);

        // ResolverEquipeDoTecnicoQuery é sempre Result.Success (Value pode ser null, ausência
        // de vínculo não é falha — ver XML doc do próprio handler em Catalogo).
        return resultado.Value;
    }
}
