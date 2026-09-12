# Autorização por recurso

## As duas leituras de "só X pode fazer Y"

Uma regra de autorização quase sempre tem duas leituras distintas, resolvidas em lugares diferentes:

| Leitura | Exemplo | Mecanismo | Onde vive |
| --- | --- | --- | --- |
| A: papel genérico, não depende do conteúdo da requisição | "Só usuário com papel X pode chamar este endpoint" | `[Authorize(Roles = "...")]` | Camada Api (nativo do framework, sem código customizado) |
| B: depende do conteúdo do `Command`/dado de domínio | "Só o usuário pode agir sobre o próprio recurso" / "só quem está vinculado a este recurso específico pode agir sobre ele" | Pipeline behavior de autorização | Application (mesma mecânica de `ValidationBehavior`/`LoggingBehavior`) |

A leitura A não precisa de nada além do que o framework já oferece. A leitura B é onde vale um mecanismo próprio, porque o atributo declarativo não enxerga o conteúdo do `Command` antes dele estar montado e prestes a ser despachado.

Exemplo da leitura A, resolvida inteiramente na Api, sem tocar o pipeline de `Command`:

```csharp
[ApiController]
[Route("api/v1/recursos")]
public sealed class RecursosController : ControllerBase
{
    private readonly IMediator _mediator;

    public RecursosController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{recursoId:guid}/operacoes-administrativas")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ExecutarOperacaoAdministrativa(
        Guid recursoId,
        ExecutarOperacaoAdministrativaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ExecutarOperacaoAdministrativaCommand(recursoId, request.Motivo),
            cancellationToken);

        return result.ToActionResult();
    }
}
```

O `[Authorize(Roles = "Administrador")]` rejeita a requisição com 403 antes mesmo do controller ser instanciado: nenhum `Command` chega a ser montado, nenhum `IRequiresAuthorization` roda. É a checagem certa quando o critério é só "qual papel o ator tem", sem depender de qual `recursoId` está na rota. Se a regra também precisasse saber se o ator está vinculado a este `recursoId` específico, deixaria de ser leitura A e passaria a ser leitura B: o próprio `Command` implementaria `IRequiresAuthorization`, como no `AlterarRecursoComVinculoCommand` abaixo.

## Mecanismo

A leitura B é resolvida por uma interface marcadora combinada com pipeline behavior: o `Command` implementa `IRequiresAuthorization`, e um `AuthorizationBehavior` no pipeline chama essa checagem antes do handler rodar.

A checagem recebe dois colaboradores, e só dois: o `ICurrentUser` (identidade pura de quem pede, ver [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md)) e um `IAuthorizationContext`, serviço estreito que responde perguntas de vínculo ator↔recurso contra a infraestrutura. Manter a consulta de vínculo num serviço dedicado — em vez de pendurá-la no próprio `ICurrentUser` — é o que impede a identidade de virar um objeto que sabe consultar banco e conhece conceitos de domínio como "recurso":

```csharp
public interface IRequiresAuthorization
{
    // Recebe identidade (ICurrentUser) e um serviço de consulta de vínculo (IAuthorizationContext),
    // nunca um IServiceProvider genérico: as dependências da checagem ficam explícitas na assinatura.
    Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken);
}

// Serviço estreito, implementado na Infrastructure; só o que a autorização por recurso precisa perguntar.
public interface IAuthorizationContext
{
    Task<bool> HasResourceLinkAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken);
}

public sealed class AuthorizationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IRequiresAuthorization
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationContext _context;

    public AuthorizationBehavior(ICurrentUser currentUser, IAuthorizationContext context)
    {
        _currentUser = currentUser;
        _context = context;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!await message.IsAuthorizedAsync(_currentUser, _context, cancellationToken))
            throw new AuthorizationDeniedException("Usuário não tem permissão para executar esta operação.");

        return await next(message, cancellationToken);
    }
}
```

Quando `IsAuthorizedAsync` devolve `false`, o behavior lança `AuthorizationDeniedException` e **não chama `next`**: o handler nunca executa, o agregado nunca é carregado, nenhum efeito colateral acontece. A exceção propaga até o `GlobalExceptionHandler`, que a traduz para 403 (ver tabela em [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)). Não existe caminho onde a checagem falha e o handler roda mesmo assim: `AuthorizationBehavior` vem antes do handler na ordem fixa do pipeline (`Logging → Validation → Authorization → Idempotency → Caching → Handler`), então uma autorização negada nunca chega a consumir uma `Idempotency-Key` nem a popular cache.

O `Command` implementa a checagem comparando o próprio conteúdo com o usuário autenticado, ou consultando um dado de infraestrutura relevante (ex.: um vínculo entre o usuário e o recurso):

```csharp
// "Recurso próprio": a checagem compara só o conteúdo do Command com a identidade — não toca o banco,
// então ignora o IAuthorizationContext. O UsuarioId do Command é o dono; basta ser o próprio ator.
public sealed record AlterarProprioPerfilCommand(Guid UsuarioId, /* ... */)
    : IRequest<Result<Guid>>, IRequiresAuthorization
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.UserId == UsuarioId);
}

// "Recurso com vínculo": a checagem precisa consultar a infraestrutura — é aí que o IAuthorizationContext entra.
public sealed record AlterarRecursoComVinculoCommand(Guid RecursoId, /* ... */)
    : IRequest<Result<Guid>>, IRequiresAuthorization
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("PapelNecessario"))
            return false;

        return await context.HasResourceLinkAsync(currentUser.UserId, RecursoId, cancellationToken);
    }
}
```

## Por que não colocar isso dentro do agregado

Se o agregado recebesse o identificador do ator e validasse o vínculo por dentro:

- O agregado passaria a depender de um conceito (o vínculo ator↔recurso) que é dado de infraestrutura/cadastro, não uma regra intrínseca de "o que é uma transição de estado válida".
- Testar o agregado isoladamente (sem banco, sem repositório) ficaria impossível para esse caminho.
- Um processo administrativo legítimo (reprocessamento em lote, correção de dados) precisaria forjar um ator específico só para satisfazer a checagem: a regra de permissão vazou para dentro de um método que deveria continuar utilizável por qualquer chamador autorizado a montá-lo.

O agregado nunca sabe que a checagem de autorização existe: continua recebendo só dados de negócio e validando só os próprios invariantes. A autorização já foi resolvida antes do handler carregar o agregado. Ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md), "onde a regra de negócio não mora".

## Quando as duas leituras parecem se sobrepor

Há uma distinção que fica confusa quando o mesmo verbo ("pode fazer X?") aparece nas duas camadas. O teste de `02-dominio-hibrido.md` resolve isso: **a regra continuaria valendo se a operação fosse disparada por um processo automatizado, sem usuário logado?**

- Se sim, é invariante de domínio: depende só do **estado do próprio recurso**, mora no agregado.
- Se não, é autorização: depende de **quem pede**, nunca mora no agregado.

Exemplo abstrato lado a lado, sobre o mesmo `Agregado` e a mesma `Operacao`:

|  | Invariante de domínio (mora no agregado) | Autorização (mora no `Command`) |
| --- | --- | --- |
| Pergunta | "Este `Agregado`, no estado em que está, aceita esta `Operacao`?" | "Este ator específico pode disparar esta `Operacao` sobre este `Agregado`?" |
| Depende de | Estado interno do próprio `Agregado` (ex.: `Situacao`, datas, quantidades já registradas) | Identidade/papel/vínculo de quem chama, nunca exposto ao agregado |
| Continua valendo sem usuário logado? | Sim, um job de reprocessamento em lote dispararia a mesma rejeição | Não, um job administrativo não tem "ator", e a operação ainda deve ser permitida para ele |
| Exemplo de violação | `Agregado` já está `Encerrado`; `Operacao` de alteração é rejeitada por `DomainException` | Ator não tem vínculo com este `Agregado`; `Operacao` é rejeitada por `AuthorizationDeniedException` |
| Onde o teste unitário mora | Domain, sem mock de usuário/repositório | Application, testando `Command.IsAuthorizedAsync` isoladamente |

As duas checagens rodam para a mesma requisição, uma depois da outra, e nenhuma substitui a outra: autorização primeiro (no pipeline, antes do handler carregar o agregado), invariante de domínio depois (dentro do método de ação do agregado, já carregado). Um ator pode ter permissão plena sobre um `Agregado` e mesmo assim ter a `Operacao` rejeitada porque o estado atual não permite: as duas respostas são independentes e a ordem não é arbitrária, não faz sentido carregar o agregado para validar estado antes de saber se o ator tinha o direito de pedir a operação.

Exemplo abstrato combinando as duas camadas na mesma operação. O agregado só sabe validar o próprio estado:

```csharp
public sealed class Agregado
{
    public Guid Id { get; }
    public SituacaoAgregado Situacao { get; private set; }

    public void ExecutarOperacao()
    {
        if (Situacao == SituacaoAgregado.Encerrado)
            throw new DomainException("Agregado encerrado não aceita esta operação.");

        // ... transição de estado ...
    }
}
```

O `Command` só sabe validar quem pede: nunca vê `Situacao`, nunca precisa saber que o invariante existe.

```csharp
public sealed record ExecutarOperacaoCommand(Guid AgregadoId)
    : IRequest<Result<Unit>>, IRequiresAuthorization
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        await context.HasResourceLinkAsync(currentUser.UserId, AgregadoId, cancellationToken);
}
```

O handler só junta as duas peças, cada uma resolvida no seu lugar: a autorização já rodou no pipeline antes deste código ser alcançado, então aqui só resta o invariante de domínio.

```csharp
public sealed class ExecutarOperacaoHandler : IRequestHandler<ExecutarOperacaoCommand, Result<Unit>>
{
    private readonly IAgregadoRepository _repository;
    private readonly ICurrentUser _currentUser;

    public ExecutarOperacaoHandler(IAgregadoRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<Unit>> Handle(ExecutarOperacaoCommand command, CancellationToken cancellationToken)
    {
        var agregado = await _repository.ObterPorAtorAsync(
            _currentUser.UserId, command.AgregadoId, cancellationToken);

        try
        {
            agregado.ExecutarOperacao();
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(agregado, cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

Se o vínculo ator↔recurso não existisse, `AuthorizationBehavior` já teria barrado a requisição com 403 antes do handler ser chamado: este código só roda para quem já tem o direito de pedir a operação, e mesmo assim ainda pode falhar por `DomainException` se o estado do `Agregado` não permitir.

**Caso comum: "usuário só vê/acessa os próprios recursos".** Aqui as duas leituras deste documento e o filtro por linha de [`14-filtro-de-dados.md`](14-filtro-de-dados.md#1-filtro-por-linha) normalmente andam juntos, cada um cobrindo uma via de acesso diferente ao mesmo recurso:

- **Acesso direto por id** (`ObterPorId`, `Executar`, `Alterar` recebendo um `RecursoId` explícito): quem barra é `AuthorizationBehavior`/`IRequiresAuthorization`, no pipeline, antes do handler carregar qualquer coisa. Sem essa checagem, um ator poderia adivinhar/manipular o id de um recurso alheio na requisição e, se o repositório não tivesse escopo, carregá-lo.
- **Listagem** (`ObterMeusRecursos`, qualquer `Query` que devolve vários registros): não há um único id para autorizar contra. O que impede o vazamento é o repositório nunca oferecer uma consulta sem escopo por ator, conforme o padrão de assinatura obrigatória de `14-filtro-de-dados.md`.

Em outras palavras: autorização por recurso nega a operação no pipeline antes do handler rodar; filtro por linha impede o dado de ser carregado do banco em primeiro lugar. Para o caminho de acesso direto por id, as duas checagens tendem a se sobrepor (o mesmo `RecursoId` seria barrado pelas duas), e isso é esperado: redundância de defesa em profundidade, não duplicação a ser eliminada. Para listagem, só o filtro por linha se aplica, porque não existe um id único de recurso para `IRequiresAuthorization` validar.

Note que `ObterPorAtorAsync` recebe `_currentUser.UserId` como parâmetro obrigatório: o repositório já segue o escopo obrigatório por assinatura de [`14-filtro-de-dados.md`](14-filtro-de-dados.md#1-filtro-por-linha). As duas checagens não competem entre si: `AuthorizationBehavior` decide **se** o ator pode disparar a operação (nega no pipeline, antes de qualquer consulta ao banco); o escopo do repositório garante que a consulta em si **nunca carrega** um agregado fora do escopo do ator, mesmo que algum outro caminho de código chame o handler sem passar pelo pipeline. Uma camada não substitui a outra.

## `AuthorizationDeniedException` → 403

Ver tabela completa em [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md).

## Autorização entre módulos

Quando um `Command`/`Query` é despachado de um módulo para outro (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), a checagem de `IRequiresAuthorization` roda no módulo **dono** do `Command`, não no módulo chamador: o `ICurrentUser` chega até lá porque é resolvido uma vez, no início do pipeline HTTP, e propagado por injeção de dependência (`Scoped`), não por parâmetro manual. O módulo chamador não replica a checagem de autorização do módulo dono.

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): onde `AuthorizationBehavior` entra na ordem do pipeline
- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): por que a permissão não mora no agregado, teste para distinguir invariante de domínio de autorização
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): propagação de `ICurrentUser` entre módulos
