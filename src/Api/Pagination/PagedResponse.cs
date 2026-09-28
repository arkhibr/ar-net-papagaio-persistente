using SharedKernel;

namespace Api.Pagination;

/// <summary>
/// Envelope de paginação (arquitetura/19-paginacao.md). totalItems é a contagem real que o
/// usuário pode ver, calculada pela Query com o mesmo filtro de linha da página (M8 de achados.md).
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PagedResponse<T> De(PagedResult<T> pagina, int page, int pageSize) => new(
        pagina.Items,
        page,
        pageSize,
        pagina.TotalItems,
        (int)Math.Ceiling(pagina.TotalItems / (double)pageSize));
}
