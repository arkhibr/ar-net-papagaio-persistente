namespace Api.Pagination;

/// <summary>
/// Envelope técnico de paginação (arquitetura/19-paginacao.md): nomes em inglês mesmo com
/// domínio em português — "paginação é mecânica de transporte, não vocabulário de domínio".
/// Só os campos DENTRO de items seguem a língua de domínio (ex.: ChamadoResumoDto).
///
/// Onde o envelope é montado (decisão desta rodada, não estava 100% explícita no plano):
/// MeusChamadosQuery/FilaDaEquipeQuery (Chamados.Application, já implementadas) devolvem só
/// Result&lt;IReadOnlyList&lt;ChamadoResumoDto&gt;&gt; — sem TotalItems. O repositório
/// (IChamadoRepository, Chamados.Application/Infrastructure) também não expõe nenhum método de
/// contagem hoje. Refazer a Query/handler para devolver um envelope teria sido mudar uma camada
/// já implementada e testada fora do escopo desta etapa (Api). Por isso o envelope é montado
/// aqui, na Api, com um TotalItems best-effort:
///   - Se items.Count == pageSize, a próxima página PODE existir: TotalItems é reportado como
///     desconhecido de forma honesta (usa o menor valor que os dados confirmam: page * pageSize,
///     nunca inventa um total maior) e TotalPages soma +1 sobre a página atual como sinalização
///     de "há mais", sem fingir precisão que a Application não fornece.
///   - Se items.Count &lt; pageSize (última página, comum ou vazia), TotalItems é exato:
///     (page - 1) * pageSize + items.Count.
/// Esta é uma aproximação deliberada, não o "totalItems exato reflete o resultado já filtrado"
/// que arquitetura/19-paginacao.md pede como ideal — fica registrado aqui como pendência: a
/// forma correta de fechar essa lacuna é IChamadoRepository ganhar um ContarPorX (mesma
/// consulta, sem Skip/Take) e a Query devolver um DTO com TotalItems, migrando o envelope para
/// dentro de Chamados.Application. Não fiz essa mudança agora por não ser o escopo desta etapa
/// (Api), mas o comentário fica registrado como próximo passo, não como "resolvido".
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PagedResponse<T> DeListaSemContagemTotal(IReadOnlyList<T> items, int page, int pageSize)
    {
        var itensAteEstaPagina = ((page - 1) * pageSize) + items.Count;

        if (items.Count < pageSize)
        {
            // Última página confirmada: total é exato.
            var totalPagesExato = pageSize == 0 ? 0 : (int)Math.Ceiling(itensAteEstaPagina / (double)pageSize);
            return new PagedResponse<T>(items, page, pageSize, itensAteEstaPagina, Math.Max(totalPagesExato, page));
        }

        // Página cheia: pode haver mais além dela. Reporta o mínimo confirmado (nunca inventa
        // um total maior que o confirmado) e sinaliza pelo menos +1 página.
        return new PagedResponse<T>(items, page, pageSize, itensAteEstaPagina, page + 1);
    }
}
