using Catalogo.Contracts;
using Catalogo.Domain;

namespace Catalogo.Application;

/// <summary>Equipes de atendimento. Mesma divisão leitura/escrita de ICategoriaDeServicoRepository.</summary>
internal interface IEquipeRepository
{
    Task<IReadOnlyList<EquipeDto>> ListarComMembrosAsync(CancellationToken cancellationToken);

    Task<bool> ExisteAsync(Guid equipeId, CancellationToken cancellationToken);

    Task AdicionarAsync(Equipe equipe, CancellationToken cancellationToken);

    Task<Equipe?> ObterParaEscritaAsync(Guid equipeId, CancellationToken cancellationToken);
}
