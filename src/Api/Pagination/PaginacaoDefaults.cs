namespace Api.Pagination;

/// <summary>
/// pageSize máximo e default impostos no servidor, nunca decididos pelo cliente
/// (arquitetura/19-paginacao.md, "sem limite é vetor barato de negação de serviço"). Único
/// lugar da Api que normaliza page/pageSize antes de despachar MeusChamadosQuery/
/// FilaDaEquipeQuery — os dois únicos endpoints paginados desta rodada
/// (plano-de-arquitetura.md secao 6).
/// </summary>
public static class PaginacaoDefaults
{
    public const int PageSizeDefault = 20;
    public const int PageSizeMaximo = 100;

    public static (int Page, int PageSize) Normalizar(int page, int pageSize)
    {
        var paginaValida = page < 1 ? 1 : page;

        var tamanhoValido = pageSize switch
        {
            <= 0 => PageSizeDefault,
            > PageSizeMaximo => PageSizeMaximo,
            _ => pageSize,
        };

        return (paginaValida, tamanhoValido);
    }
}
