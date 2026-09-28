namespace SharedKernel;

/// <summary>
/// Página de uma Query paginada com a contagem total que o usuário pode ver, calculada com o
/// mesmo filtro de linha da página (arquitetura/19-paginacao.md, "Ordem de aplicação").
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems);
