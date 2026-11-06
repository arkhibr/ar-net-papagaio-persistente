using Catalogo.Application;
using Catalogo.Contracts;
using Catalogo.Domain;

namespace Catalogo.Application.UnitTests.Fakes;

/// <summary>
/// Fake in-memory de ICategoriaDeServicoRepository. Guarda o agregado de verdade e projeta para
/// o formato da porta (DTO / tupla equipe+SLA), como a implementação real faz no banco
/// (arquitetura/27, "Projeção direta para DTO").
/// </summary>
internal sealed class FakeCategoriaDeServicoRepository : ICategoriaDeServicoRepository
{
    private readonly Dictionary<Guid, CategoriaDeServico> _categorias = new();

    public FakeCategoriaDeServicoRepository ComCategoria(CategoriaDeServico categoria)
    {
        _categorias[categoria.Id] = categoria;
        return this;
    }

    public Task<IReadOnlyList<CategoriaDeServicoDto>> ListarAsync(bool incluirInativas, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoriaDeServicoDto>>(
            _categorias.Values
                .Where(c => incluirInativas || c.Ativa)
                .Select(c => new CategoriaDeServicoDto(
                    c.Id, c.Nome, c.EquipeId, null, c.Ativa, c.Slas.Select(s => new SlaDto(s.Prioridade, s.Horas)).ToList()))
                .ToList());

    public Task<(Guid EquipeId, int? HorasDeSla, bool Ativa)?> ObterEquipeESlaAsync(
        Guid categoriaId, PrioridadeServico prioridade, CancellationToken cancellationToken) =>
        Task.FromResult<(Guid EquipeId, int? HorasDeSla, bool Ativa)?>(
            _categorias.TryGetValue(categoriaId, out var categoria)
                ? (categoria.EquipeId, categoria.HorasDeSlaPara(prioridade), categoria.Ativa)
                : null);

    public Task AdicionarAsync(CategoriaDeServico categoria, CancellationToken cancellationToken)
    {
        _categorias[categoria.Id] = categoria;
        return Task.CompletedTask;
    }

    public Task<CategoriaDeServico?> ObterParaEscritaAsync(Guid categoriaId, CancellationToken cancellationToken) =>
        Task.FromResult(_categorias.GetValueOrDefault(categoriaId));
}
