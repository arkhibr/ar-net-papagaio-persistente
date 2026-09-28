using Catalogo.Contracts;
using Catalogo.Domain;

namespace Catalogo.Application;

/// <summary>
/// Categorias de serviço. Leitura por projeção direta no banco, sem materializar o agregado
/// (arquitetura/27); escrita carrega o agregado rastreado, e o commit é do UnitOfWorkBehavior
/// (arquitetura/25).
/// </summary>
internal interface ICategoriaDeServicoRepository
{
    Task<IReadOnlyList<CategoriaDeServicoDto>> ListarAsync(bool incluirInativas, CancellationToken cancellationToken);

    /// <summary>Equipe da categoria, horas de SLA da prioridade (null se não houver) e se a categoria está ativa.</summary>
    Task<(Guid EquipeId, int? HorasDeSla, bool Ativa)?> ObterEquipeESlaAsync(
        Guid categoriaId, PrioridadeServico prioridade, CancellationToken cancellationToken);

    Task AdicionarAsync(CategoriaDeServico categoria, CancellationToken cancellationToken);

    Task<CategoriaDeServico?> ObterParaEscritaAsync(Guid categoriaId, CancellationToken cancellationToken);
}
