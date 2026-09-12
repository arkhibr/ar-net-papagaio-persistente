# Leitura e o query side dentro do módulo

> O lado "Q" do CQRS **dentro de um único módulo**: como uma `Query` lê, projeta e devolve dado, sem tocar no agregado rico de escrita. Complementa [`03-commands-e-queries.md`](03-commands-e-queries.md) (o mecanismo de despacho de Command/Query), [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) (que cobre a leitura **entre** módulos — read model dedicado, composição na borda, `Query` do `Contracts` alheio) e as etapas de [`14-filtro-de-dados.md`](14-filtro-de-dados.md) e [`19-paginacao.md`](19-paginacao.md), que se aplicam antes/depois da projeção descrita aqui.
> **Não é** sobre: quando usar Command vs. serviço direto (isso é `03`); leitura cruzando `Contracts` de outro módulo (isso é `04`); política de cache (isso é `24-cache.md`); ordenação/envelope de página (isso é `19`).

## Regra direta: toda `Query` lê sem rastreamento

**Regra direta: toda `Query` lê com `AsNoTracking` por padrão.** Leitura não muda estado, logo não precisa de change tracking — manter o entity tracker povoado numa consulta que nunca vai chamar `SaveChanges` é custo puro (memória e o snapshot que o EF Core monta para detectar mudanças). Rastreamento é a **exceção do lado de escrita**: um `Command` que carrega um agregado para mutá-lo precisa que o `DbContext` rastreie a entidade, porque é isso que faz `SaveChanges` gerar o `UPDATE`. O `Command` rastreia; a `Query` não.

```csharp
// {Modulo}.Application/Registros/ObterRegistroQueryHandler.cs
internal sealed class ObterRegistroQueryHandler
    : IRequestHandler<ObterRegistroQuery, Result<RegistroDto>>
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public async ValueTask<Result<RegistroDto>> Handle(
        ObterRegistroQuery query, CancellationToken ct)
    {
        var dto = await _dbContext.Registros
            .AsNoTracking()                              // leitura não rastreia
            .Where(r => r.AtorId == _currentUser.UserId) // escopo obrigatório por linha (doc 14)
            .Where(r => r.Id == query.RegistroId)
            .Select(r => new RegistroDto(r.Id, r.Titulo, r.CriadoEm)) // projeção no banco
            .SingleOrDefaultAsync(ct);

        return dto is null ? Result.NotFound() : Result.Success(dto);
    }
}
```

A `Query` é despachada via `ISender.Send(...)`, como qualquer outro artefato do pipeline (ver [`03-commands-e-queries.md`](03-commands-e-queries.md)).

## Projeção direta para DTO no banco

**Regra direta: a `Query` projeta com `.Select(...)` para o DTO de leitura, e a projeção acontece no banco.** Nunca carregue o agregado inteiro para depois mapear em memória quando só alguns campos importam. A projeção via `.Select(...)` para um `record` de leitura faz o EF Core traduzir o mapeamento para o `SELECT` — só as colunas do DTO trafegam, e o agregado nunca é materializado.

| Abordagem | O que acontece no banco | Custo |
| --- | --- | --- |
| `AsNoTracking().Select(dto)` | `SELECT` só das colunas do DTO | Mínimo: só o que a tela usa |
| Carregar agregado e mapear em memória | `SELECT *` da(s) tabela(s) do agregado, inclusive `Include` de coleções | Colunas e linhas a mais, materialização do agregado, mapeamento manual depois |

O segundo caminho só se justifica quando a leitura de fato precisa do agregado completo (raro no query side) — e mesmo aí continua sendo `AsNoTracking`.

**Pegadinha:** o `.Select(...)` que projeta para o DTO tem que ser traduzível pelo provider. Chamar um método C# arbitrário dentro do `.Select` (ex.: um construtor que roda lógica, ou um método de formatação) força avaliação em cliente ou quebra a tradução. Mantenha a projeção como atribuição direta de coluna para propriedade; formatação de apresentação é responsabilidade de quem consome o DTO, não da query.

## DTO de leitura vs. agregado de domínio

O lado de leitura tem seus **próprios tipos de leitura** (read DTOs / read records), desacoplados do agregado. A leitura não precisa passar pelo agregado rico: o agregado existe para proteger invariantes durante a **escrita** (ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md)), e uma consulta que só devolve dado para uma tela não tem invariante para proteger. Forçar toda leitura a hidratar o agregado acopla o formato de resposta à modelagem interna de escrita e paga o custo de materializar comportamento que ninguém vai invocar.

**Onde esses DTOs moram:** o read DTO devolvido por uma `Query` interna do módulo mora na `Application` do módulo, junto do handler que o produz. Quando o mesmo tipo de leitura precisa atravessar a fronteira do módulo (uma `Query` exposta em `{Modulo}.Contracts`, ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), o DTO mora no `Contracts`, porque passa a ser parte da superfície pública. A regra é a mesma de qualquer tipo: ele mora na camada mais interna que ainda o expõe.

**Nível de confiança:** alto para "leitura não precisa passar pelo agregado" (é o cerne do CQRS e coerente com `02`); a fronteira exata de onde o read DTO mora (Application vs. Contracts) segue a mesma regra de visibilidade de `01`/`04`, sem novidade.

## Relação com o filtro de dados (doc 14)

Filtro por linha e filtro por campo (ver [`14-filtro-de-dados.md`](14-filtro-de-dados.md)) se aplicam **antes** da projeção, não depois. A ordem completa de uma listagem lida:

```text
filtro de linha (escopo obrigatório por UserId)
   → projeção (.Select para DTO)
   → ordenação + paginação (Skip/Take)   ← doc 19
```

**Regra direta: a `Query` respeita o mesmo escopo obrigatório por assinatura que o lado de escrita.** O `.Where(r => r.AtorId == _currentUser.UserId)` (ou o método de repositório que exige `UserId` como parâmetro, ver `14`) vem **antes** do `.Select`, para que a projeção só enxergue linhas que o ator pode ver. Projetar primeiro e filtrar depois inverteria a garantia: o DTO já teria sido montado a partir de linhas fora do escopo.

O escopo por linha usa `UserId` do `ICurrentUser` canônico — `{ Guid UserId; Guid SessionId; bool IsAuthenticated; bool IsSystemActor; bool IsInRole(string role); }`. O sistema não é multi-tenant; o escopo é sempre por usuário/ator.

Filtro por campo (qual DTO devolver por audiência) é decidido na `Application`, dentro do handler da `Query`, como em `14` — a projeção pode inclusive projetar direto para o DTO da audiência certa, sem materializar o DTO completo para depois redigir.

## Repositório de leitura vs. de escrita

Vale separar uma interface de leitura de uma de escrita quando as duas passam a ter formas de acesso genuinamente diferentes: a de escrita carrega o agregado rastreado (`ObterPorIdAsync` que devolve `Registro` para mutação); a de leitura devolve DTOs projetados, sem rastreamento, e talvez com paginação embutida.

| Sinal | Decisão |
| --- | --- |
| O módulo é pequeno, poucas queries, o mesmo repositório serve os dois lados sem confusão | **Não separe.** Um repositório com métodos de leitura `AsNoTracking` e métodos de escrita rastreados é aceitável |
| A interface de escrita começa a expor métodos "obter para exibir" que devolvem agregado só para alguém projetar depois | Separe: `IRegistroReadRepository` (queries projetadas) e `IRegistroRepository` (carrega agregado para mutação) |
| Muitas queries de leitura com projeções distintas, escrita concentrada em poucos casos de uso | Separe, ou deixe as queries acessarem o `DbContext` direto na `Application`, sem repositório de leitura |

**Trade-off:** separar deixa cada interface honesta sobre o que faz (a de escrita nunca vira um saco de getters), ao custo de mais um tipo. Não é obrigatório — é uma resposta a um sintoma, não um default. Acessar o `DbContext` diretamente na `Query` (como no exemplo da primeira seção) é perfeitamente aceitável no query side: não há invariante de escrita para proteger, então a indireção de um repositório de leitura só se paga quando ela remove duplicação real de projeção.

## Dapper, SQL direto e compiled queries: escalonamento por necessidade medida

LINQ + EF Core com `AsNoTracking().Select(dto)` é o default e resolve a grande maioria das leituras. Quando ele **comprovadamente** não basta — leitura quente, projeção complexa que o LINQ traduz mal, ou SQL que o profiler mostra ser dominado por overhead de tradução —, escale:

| Opção | Quando | Onde mora |
| --- | --- | --- |
| Compiled query do EF Core (`EF.CompileAsyncQuery`) | Query executada em altíssima frequência, forma fixa, o custo de recompilar a árvore de expressão aparece no profiler | `Infrastructure` do módulo |
| Dapper / SQL parametrizado | Projeção complexa, agregações, ou leitura quente onde o LINQ traduz mal e o SQL manual é mais claro e rápido | `Infrastructure` do módulo |

**Regra direta: SQL direto e Dapper vivem na `Infrastructure` do módulo e nunca cruzam o schema de outro módulo.** Uma leitura em SQL parametrizado dentro de `Academico.Infrastructure` consulta só o schema de `Academico`. Consulta que precisa de dado de outro módulo continua indo pelo `Contracts` daquele módulo, mesmo em SQL — a exceção de consulta direta multi-schema é restritiva e exige aprovação em revisão de arquitetura (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md), seção "Exceção: consulta direta multi-schema").

**Regra direta: SQL sempre parametrizado.** Concatenar valor de entrada em string de SQL é injeção; parametrize sempre, inclusive em Dapper (`connection.QueryAsync<Dto>(sql, new { atorId })`). O filtro por linha por `UserId` continua obrigatório no SQL manual — o `WHERE ator_id = @atorId` é a mesma fronteira de segurança de `14`, agora escrita à mão e igualmente inescapável.

```csharp
// {Modulo}.Infrastructure/Leitura/RelatorioRegistroReader.cs
internal sealed class RelatorioRegistroReader
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public async Task<IReadOnlyList<RegistroRelatorioDto>> ObterAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT r.id AS Id, r.titulo AS Titulo, COUNT(i.id) AS TotalItens
            FROM registros r
            LEFT JOIN itens i ON i.registro_id = r.id
            WHERE r.ator_id = @atorId          -- escopo por linha, parametrizado
            GROUP BY r.id, r.titulo
            """;

        using var conn = await _connectionFactory.OpenAsync(ct);
        var linhas = await conn.QueryAsync<RegistroRelatorioDto>(sql, new { atorId = _currentUser.UserId });
        return linhas.ToList();
    }
}
```

**Apresente estas opções como escalonamento por necessidade medida, não como default.** Adotar Dapper ou compiled query "por precaução", antes de o profiler apontar o gargalo, troca legibilidade e a segurança de tipos do LINQ por otimização que talvez nunca importe. A regra é: comece no LINQ + EF; escale só o ponto quente que a medição isolar.

## Cacheabilidade

Uma `Query` é o artefato que pode ser cacheável — é ela que passa pelo `Caching` behavior na ordem fixa do pipeline (ver [`03-commands-e-queries.md`](03-commands-e-queries.md): `... → Idempotency → Caching → Handler`). Uma `Query` que implementa o marcador de cache tem seu resultado servido do cache antes de chegar ao handler. **Este documento só aponta a cacheabilidade; a política de cache — marcador, chave, invalidação, TTL — é do [`24-cache.md`](24-cache.md).** A única nota relevante aqui: a chave de cache de uma `Query` com escopo por linha precisa incluir o `UserId`, senão o resultado de um ator vaza para outro — mas o "como" é de `24`.

## Teste (doc 13)

Leitura rastreada vs. não rastreada é caso de **teste de infraestrutura** (`Infrastructure.IntegrationTests`, contra banco real efêmero — ver [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md), "Casos que merecem teste dedicado"). O teste confirma qual método de repositório/leitura rastreia a entidade e qual não: uma entidade devolvida por uma `Query` não deve estar rastreada pelo `ChangeTracker`, enquanto a devolvida por um método de escrita deve estar. É a garantia de que a regra "toda `Query` lê com `AsNoTracking`" não regride silenciosamente quando alguém adiciona um método novo.

```csharp
// Infrastructure.IntegrationTests
[Fact]
public async Task Query_de_leitura_nao_rastreia_a_entidade()
{
    var dto = await _sender.Send(new ObterRegistroQuery(_registroExistenteId), default);

    Assert.False(dto.IsFailure);
    Assert.Empty(_dbContext.ChangeTracker.Entries()); // nada rastreado após a leitura
}
```

## Nota de aplicação

Um sistema com um perfil de leitura muito pesado (dashboards, relatórios de alto volume) pode chegar mais cedo ao ponto de separar repositórios de leitura ou de adotar Dapper de forma mais ampla — e isso é legítimo, desde que baseado em medição, não em antecipação. Do outro lado, um sistema pequeno pode nunca precisar de nada além de `AsNoTracking().Select(dto)` acessando o `DbContext` direto na `Application`, sem repositório de leitura algum. Os limiares (o que é "leitura quente", quando separar interface) são calibráveis por sistema; a única regra sem calibração é `AsNoTracking` por default e escopo por linha antes da projeção.

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): despacho de `Query` via `ISender`, ordem do pipeline e posição do `Caching` behavior
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): leitura **entre** módulos, read model dedicado e a exceção de consulta direta multi-schema
- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): filtro por linha (escopo por `UserId`) e por campo, aplicados antes da projeção
- [`19-paginacao.md`](19-paginacao.md): ordenação e paginação, etapas seguintes à projeção
- [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md): teste de infraestrutura de leitura rastreada vs. não rastreada
- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): por que o agregado rico serve a escrita, não a leitura
- [`24-cache.md`](24-cache.md): política de cache de `Query` (marcador, chave, invalidação)
