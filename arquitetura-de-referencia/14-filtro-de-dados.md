# Filtro de dados por contexto/usuário

"Ocultar dado dependendo de quem pergunta" esconde duas perguntas diferentes, resolvidas em lugares diferentes: filtro por linha (quais registros aparecem) e filtro por campo (quais campos de um registro já visível aparecem). Não confunda as duas: Specification só filtra linha; redação de DTO não impede a linha de aparecer, só oculta parte do que ela mostra.

## 1. Filtro por linha

Filtro por linha decide quais registros o usuário pode ver. Se o esquecimento de aplicar o filtro significa "usuário A vê dado do usuário B" (ou equivalente entre tenants), é fronteira de segurança, não filtro de negócio opcional. Comece pelo escopo obrigatório por assinatura do método de repositório; migre para Global Query Filter (`HasQueryFilter`, no EF Core) quando não for possível garantir que toda consulta futura passa pelo mesmo repositório.

| Mecanismo | Quando usar | Quem decide aplicar | Risco se esquecido |
| --- | --- | --- | --- |
| `Specification` opcional | Filtro de negócio opcional, o próprio usuário decide/pede | O handler, caso a caso | Resultado maior que o esperado, sem ser falha de segurança |
| `Specification` com escopo obrigatório por assinatura | Regra de visibilidade que sempre passa pelo mesmo repositório | A assinatura do método, o compilador exige o argumento | Não compila sem o argumento: o esquecimento vira erro de build |
| Global Query Filter (`HasQueryFilter`) | Fronteira de segurança obrigatória para qualquer consulta, presente ou futura | O `DbContext`, sempre, para todo mundo | N/A dentro do EF Core; só via `.IgnoreQueryFilters()` explícito |

**Pegadinha (dependente da versão do EF Core):** até o EF Core 9, `HasQueryFilter` aceita **uma única** condição por entidade — chamar de novo substitui, não combina —, então múltiplas condições devem ir num único lambda com `&&`. O EF Core 10 (nov/2025) introduziu *named query filters* (múltiplos filtros nomeados por entidade), o que remove essa limitação; ainda assim, combinar num só lambda continua válido e é o caminho compatível com as duas versões. `ICurrentUser` usado dentro do filtro precisa ter lifetime `Scoped`, nunca `Singleton`.

### Exemplo: escopo obrigatório por assinatura

O ponto central é o repositório nunca oferecer um método "obter tudo" que devolva registros de qualquer ator — o escopo é parte da assinatura, não um filtro aplicado depois em memória pelo chamador:

```csharp
public interface IRegistroRepository
{
    // O id do ator é parâmetro obrigatório: não compila uma chamada que "esqueça" o escopo.
    Task<Registro?> ObterPorIdAsync(Guid atorId, Guid registroId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Registro>> ObterPorAtorAsync(
        Guid atorId,
        Specification<Registro> filtroOpcional,
        CancellationToken cancellationToken);
}

public sealed class RegistroRepository : IRegistroRepository
{
    private readonly AppDbContext _dbContext;

    public RegistroRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<Registro?> ObterPorIdAsync(
        Guid atorId, Guid registroId, CancellationToken cancellationToken) =>
        await _dbContext.Registros
            .Where(r => r.AtorId == atorId) // escopo aplicado antes de qualquer outro filtro
            .SingleOrDefaultAsync(r => r.Id == registroId, cancellationToken);

    public async Task<IReadOnlyList<Registro>> ObterPorAtorAsync(
        Guid atorId, Specification<Registro> filtroOpcional, CancellationToken cancellationToken) =>
        await _dbContext.Registros
            .Where(r => r.AtorId == atorId)
            .Where(filtroOpcional.ToExpression())
            .ToListAsync(cancellationToken);
}
```

**Nunca faça** `ObterTodosAsync()` seguido de `.Where(r => r.AtorId == atorId)` no handler: nada impede um outro handler de chamar `ObterTodosAsync()` e esquecer o filtro. O escopo só é uma garantia quando está dentro do repositório, não do lado de quem o chama.

Como alternativa/reforço quando não é possível garantir que toda consulta passa por `IRegistroRepository` (ex.: relatórios com SQL/LINQ direto contra o `DbContext`), o Global Query Filter aplica o mesmo escopo incondicionalmente:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Registro>()
        .HasQueryFilter(r => r.AtorId == _currentUser.UserId);
}
```

### Quando o agregado filtrado não carrega o dado de visibilidade

| Cenário | Opção |
| --- | --- |
| Filtro de negócio opcional | Resolver os ids do agregado "pai" primeiro (duas consultas), filtrar o "filho" por esses ids |
| Fronteira de segurança, poucos registros por tabela | Global Query Filter com subquery correlacionada contra o `DbSet` do agregado pai |
| Fronteira de segurança, tabela grande, relação com o pai é imutável | Denormalizar o identificador como shadow property do EF Core, nunca exposta na classe de domínio |

**Nunca** adicione o campo de visibilidade na classe do agregado filho só para viabilizar o filtro: isso reintroduz no agregado um conceito que nenhuma regra de negócio dele precisa conhecer.

**Completude:** a shadow property existe só para o EF Core montar a consulta/filtro; ela nunca é mapeada para DTO nem aparece em resposta HTTP. Se um identificador de visibilidade precisa mesmo ser visível na resposta (ex.: o cliente da API precisa saber a quem o registro pertence), isso é um campo de negócio de verdade e deve ser modelado explicitamente no agregado e no DTO — não reaproveite a shadow property para os dois propósitos.

## 2. Filtro por campo

Filtro por campo decide quais campos de um registro já visível aparecem. Esse filtro não é problema do `Domain`, pois introduziria dependência de autenticação/autorização numa camada que não deveria conhecer esse conceito. A decisão vive na fronteira entre `Application` e `Api`, onde já acontece o mapeamento de agregado para DTO.

| Opção | Quando usar |
| --- | --- |
| **A: um DTO por audiência**, cada um com seu método de mapeamento | Poucos papéis, diferença pequena de campos; cada DTO é explícito sobre o que expõe só pela assinatura |
| **B: redação pós-mapeamento** (função que anula/mascara campos do DTO completo, usando `ICurrentUser`, nunca `ClaimsPrincipal`) | Muitos papéis/combinações, ou a regra de visibilidade muda com frequência e precisa ser auditável/testável num único lugar |

### Exemplo: opção A, um DTO por audiência

Dois DTOs distintos para o mesmo `Registro` — o dono do recurso vê tudo, um terceiro vê uma versão com os campos sensíveis omitidos:

```csharp
public sealed record RegistroCompletoDto(
    Guid Id,
    string Titulo,
    string Descricao,
    decimal ValorInterno,
    string DocumentoAtor,
    DateTime CriadoEm);

public sealed record RegistroResumidoDto(
    Guid Id,
    string Titulo,
    DateTime CriadoEm);
// ValorInterno e DocumentoAtor nunca existem nesta classe: não há como um mapeamento
// futuro "esquecer" de redigi-los, porque o campo simplesmente não existe no tipo.

public static class RegistroMapper
{
    public static RegistroCompletoDto ParaCompleto(Registro registro) =>
        new(registro.Id, registro.Titulo, registro.Descricao, registro.ValorInterno,
            registro.DocumentoAtor, registro.CriadoEm);

    public static RegistroResumidoDto ParaResumido(Registro registro) =>
        new(registro.Id, registro.Titulo, registro.CriadoEm);
}
```

A escolha de qual DTO devolver é decidida na `Application`, dentro do handler da `Query` — nunca no `Domain`, que não deveria saber que "dono" e "terceiro" existem. Aqui o filtro por linha (o terceiro só pode ver registros aos quais tem algum vínculo, ex.: `12-autorizacao-por-recurso.md`) já foi resolvido antes; o que resta é decidir o _formato_ da resposta:

```csharp
public sealed class ObterRegistroQueryHandler
    : IRequestHandler<ObterRegistroQuery, Result<object>>
{
    private readonly IRegistroRepository _repository;
    private readonly ICurrentUser _currentUser;

    public async ValueTask<Result<object>> Handle(ObterRegistroQuery query, CancellationToken cancellationToken)
    {
        var registro = await _repository.ObterPorIdAsync(
            _currentUser.UserId, query.RegistroId, cancellationToken);
        if (registro is null)
            return Result.NotFound();

        // A decisão de qual DTO usar mora aqui, não no Domain nem espalhada em vários handlers.
        object dto = registro.AtorId == _currentUser.UserId
            ? RegistroMapper.ParaCompleto(registro)
            : RegistroMapper.ParaResumido(registro);

        return Result.Success(dto);
    }
}
```

**Centralize toda a lógica de visibilidade de campo num único lugar** — o método de mapeamento do DTO específico da audiência (opção A) ou a função de redação pós-mapeamento (opção B) — nunca espalhada pelo handler. Evite `if (usuarioAtual.IsInRole(...))` repetido dentro do handler ou, pior, dentro do agregado de domínio: isso reintroduz o acoplamento entre regra de negócio e autorização que [`02-dominio-hibrido.md`](02-dominio-hibrido.md) evita, e espalha a mesma decisão de visibilidade por vários pontos que precisam ser lembrados e mantidos em sincronia manualmente.

**Isso não é autorização formalizada como `IRequiresAuthorization`, e está certo que não seja.** A comparação `registro.AtorId == _currentUser.UserId` no handler acima decide só qual **DTO devolver** numa `Query` de leitura — o registro já foi carregado dentro do escopo do ator (filtro por linha) e nenhuma das duas alternativas de resposta muda estado ou concede acesso a dado fora desse escopo; na pior hipótese, o ator vê a versão resumida de algo que já tinha o direito de enxergar de algum modo. Formalize a mesma checagem como `IRequiresAuthorization` (ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)) quando a operação **muda estado** — aí o custo de errar deixa de ser "formato de resposta incorreto" e passa a ser "efeito colateral indevido", e a checagem precisa rodar no pipeline, antes do handler, não depois de o dado já ter sido carregado e uma decisão de negócio já ter sido tomada.

## Filtro de dados entre módulos

Quando um módulo despacha uma `Query` contra o `Contracts` de outro (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), o filtro por linha e por campo já acontece **dentro** do módulo dono, antes do dado sair pelo `Contracts`. Isso é consequência direta de `Contracts` ser a única superfície pública: o dado que atravessa essa fronteira já respeita a visibilidade decidida pelo módulo dono, com o mesmo `ICurrentUser` propagado pela requisição original.

O módulo chamador, por sua vez, nunca recebe um DTO "cru" que ele mesmo precisaria redigir de novo. Ele não replica a lógica de filtro por campo do módulo dono — receber um DTO já filtrado é a garantia; refiltrar do lado de quem chama seria duplicar uma decisão que já foi tomada, com risco de essa segunda cópia divergir da primeira com o tempo.

## Veja também

- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): por que filtro de campo não mora no agregado
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): onde o filtro acontece quando o dado atravessa `Contracts`
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): leitura A (papel genérico) vs. leitura B (dependente de dado): mesmo critério aplicado aqui à decisão de filtro
- [`15-observabilidade.md`](15-observabilidade.md): mesmo critério de visibilidade de campo, reaplicado ao que não deveria ir para o log
