# Estrutura de projetos para monólito modular

## Contexto

Não existe padrão único na comunidade .NET para quantos projetos usar por módulo de um monólito modular: a contagem varia de 4 (Kamil Grzybek, Milan Jovanović) a 2 (Ardalis `Modulith`, ABP Framework). O que todas as fontes tratam como não negociável não é a contagem de projetos, e sim a fronteira entre módulos e como ela é garantida. Este documento separa as duas coisas.

## Conceito de módulo

Um módulo é um bounded context completo dentro da solution: dono de um pedaço do domínio de negócio, com dado próprio (schema/tabelas que só ele mapeia), agregado(s) próprio(s), e o conjunto de casos de uso que resolve sozinho. Nenhum módulo lê ou escreve o schema de outro módulo, nem referencia o tipo de domínio de outro diretamente — a única forma de um módulo saber de outro é através do `Contracts` desse outro (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)). Um sistema pode ter um módulo só (a maioria começa assim) ou vários; a fronteira vale da mesma forma nos dois casos — promover de "um módulo" para "dois módulos" é adicionar um novo `{Modulo}`/`{Modulo}.Contracts` ao lado do que já existe, nunca reescrever a forma como o primeiro módulo acessa dado.

## Fronteira entre módulos

A fronteira entre módulos é fixa, igual em todo módulo, sem exceção.

- Um projeto `{Modulo}.Contracts` é a única superfície visível de fora do módulo (eventos de integração, DTOs, e a interface pública de fachada quando comunicação síncrona entre módulos for permitida). Sem entidade de domínio, sem `DbContext`, sem handler.
- Todo o resto do módulo é `internal` por padrão.
- Uma suíte de testes arquiteturais (`NetArchTest` ou `ArchUnitNET`) valida, no mínimo: nenhum módulo referencia o interior de outro módulo além de `Contracts`; `Api` também só referencia `{Modulo}.Contracts` de cada módulo, nunca o interior de nenhum; todo tipo fora de `Contracts` é de fato `internal`.

Essa parte não varia por módulo nem por sistema: é o que preserva a opção de extrair um módulo depois e o que qualquer pessoa, em qualquer sistema da organização que siga este padrão, reconhece sem precisar reaprender.

**Hierarquia de garantia, do mecanismo mais forte ao mais fraco:** compilador (referência de projeto + `internal`), depois teste arquitetural (pega o que o compilador não alcança, incluindo alguém adicionando uma referência "só dessa vez"), depois code review (mais lento e mais falível que os dois anteriores). Como não há legado nem suíte de testes herdada num monólito modular novo, o teste arquitetural nasce junto com o primeiro módulo. Adiar essa suíte para "quando houver mais de um módulo" é o cenário que a pesquisa de origem chama de _boundary theater_: pasta e convenção sem teste real por trás, o padrão de falha mais comum em modularização.

**O primeiro módulo de produção não está completo sem essa suíte já implementada.** Não é um item de backlog para "depois que o segundo módulo aparecer": um módulo entregue sem o teste arquitetural de fronteira é trabalho inacabado, mesmo que compile, passe nos testes de negócio e esteja em produção.

## Granularidade interna do módulo

A granularidade interna do módulo é variável, decidida por sinal objetivo, não por contagem fixa. Ponto de partida padrão para qualquer módulo novo: **2 projetos de produção**: `{Modulo}` (`Domain` + `Application` + `Infrastructure` fundidos, tudo `internal`) e `{Modulo}.Contracts`, mais o(s) projeto(s) de teste (inventário completo e critério de separação por camada em [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md); resumindo, o de domínio/aplicação precisa de `InternalsVisibleTo` do assembly `{Modulo}` para enxergar os tipos `internal` que testa).

Promova um módulo para a estrutura completa (`Domain`, `Application`, `Infrastructure` e `Contracts` separados) quando pelo menos dois destes sinais estiverem presentes:

1. Múltiplos agregados com regra de negócio não trivial ou máquina de estados complexa.
2. Volume de casos de uso distintos crescendo além do que uma pasta única comporta com clareza.
3. Candidato real e próximo de extração como serviço independente.
4. Mais de uma pessoa ou subtime mantendo o módulo simultaneamente, precisando de fronteira interna mais rígida que convenção de pasta.

Promoção de 2 para 4 projetos é reorganização de pastas dentro do mesmo assembly, sem quebra de contrato, desde que a fronteira da seção anterior já esteja correta desde o início: é uma mudança deliberadamente barata. Rebaixar de 4 para 2 é mais raro e mais arriscado; na dúvida, o ponto de partida enxuto é o mais seguro.

**Nota:** este critério decide granularidade _interna_ de um módulo (quantos agregados, quantos casos de uso, quantas pessoas mantendo o código), ou seja, como o módulo se organiza por dentro. É uma decisão independente de quando uma composição de dados na borda entre módulos deve virar um read model dedicado, que é sobre escalabilidade de leitura _entre_ módulos e está coberta em [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md). Os dois critérios podem apontar para direções diferentes no mesmo módulo ao mesmo tempo, sem contradição.

**Nível de confiança:** este critério de promoção é síntese própria a partir de heurísticas soltas das fontes pesquisadas (tamanho de time, CRUD vs. domínio rico, chance de extração); nenhuma fonte oferece uma checklist pronta como esta. Precisa de validação por quem tem visão dos sistemas reais adotando o padrão, e não deve ser aceito só por estar escrito aqui.

## Api: ponto de partida único, promovível por módulo

A solution começa com um único projeto `Api`, não um por módulo: mais simples enquanto há poucos módulos e poucos times, porque centraliza num lugar só o que é cross-cutting de qualquer módulo (sessão, CSRF, exception handling), sem duplicar essa plumbing. Ele é, ao mesmo tempo, o composition root (registra a `DependencyInjection` de cada módulo presente na solution) e cumpre o papel de BFF (autenticação, sessão, CSRF — ver [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) e [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md)). `Api` referencia só o `{Modulo}.Contracts` de cada módulo que expõe endpoint, nunca o interior de nenhum módulo — a mesma fronteira fixa da seção "Fronteira entre módulos" vale para `Api` tanto quanto para um módulo referenciando outro.

**Promova controllers para dentro do módulo pelos mesmos sinais que promovem a granularidade interna** (seção anterior: mais de um time, volume de casos de uso, candidato a extração). Nessa promoção, cada módulo ganha seu próprio `{Modulo}.Api`, referenciando só `{Modulo}.Contracts` e `SharedKernel` — a mesma fronteira de sempre, só o dono físico do controller muda. O processo continua único: `Api` deixa de conter controller nenhum e vira só o host (`Program.cs`), que agrega os assemblies de controller de cada `{Modulo}.Api` via Application Parts do ASP.NET Core, e mantém centralizado só o que é de fato cross-cutting (pipeline de sessão/CSRF, health check agregado, composição entre módulos — ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md), seção "Composição na borda"). Times diferentes passam a mexer no controller do próprio módulo sem disputar o mesmo projeto `Api`.

## SharedKernel: o que é compartilhado entre tudo

Um projeto `SharedKernel` (o nome exato é escolha do sistema concreto) contém o que todo módulo e o `Api` precisam para existir, mas que não é regra de negócio de nenhum módulo específico:

- `Result<T>` e o tipo de falha que ele carrega.
- Classe-base de `DomainException`.
- Interfaces que a `Application` de qualquer módulo depende, mas cuja implementação concreta é responsabilidade da composição raiz: `ICurrentUser` (com um marcador explícito de ator automatizado, ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), `ICurrentUserAccessor` (troca a identidade ambiente para um handler reativo assumir ator de sistema), `TimeProvider` (o uso da abstração, não a implementação).
- Marcadores/interfaces de pipeline behavior: `IRequiresAuthorization`, `IIdempotentCommand`, `IAuditable`.
- As implementações genéricas de pipeline behavior que rodam para qualquer módulo, sem saber nada do domínio de nenhum: `LoggingBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `IdempotencyBehavior` (ordem fixa em [`03-commands-e-queries.md`](03-commands-e-queries.md)).

**O que nunca entra em `SharedKernel`:** qualquer tipo que carregue conceito de negócio de um módulo específico — um agregado, um `Command`, um DTO. Isso é sempre `{Modulo}` ou `{Modulo}.Contracts`. `SharedKernel` é mecanismo, não domínio: se um tipo nomeia algo que o especialista de negócio reconheceria numa conversa, ele não pertence aqui (mesmo critério de língua de domínio vs. termo técnico de [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md)).

`SharedKernel` não referencia nenhum módulo nem o `Api` — é a base da pirâmide de referências da solution; todo o resto aponta para ele, ele não aponta para nada dentro da solution.

## Como `Api` compõe o sistema

Cada módulo expõe um método de extensão de `DependencyInjection` (ex.: `Add{Modulo}Module(this IServiceCollection services, IConfiguration configuration)`) que registra os próprios serviços — `DbContext`, repositórios, handlers descobertos por assembly scanning. O `Program.cs` do `Api` chama um desses métodos por módulo presente na solution, mais o registro dos pipeline behaviors do `SharedKernel` uma única vez, para o pipeline inteiro:

```csharp
// Api/Program.cs
builder.Services
    .AddSharedKernelPipeline()          // Logging, Validation, Autorização, Idempotency, Caching — uma vez só
    .AddAcademicoModule(builder.Configuration)
    .AddIdentidadeModule(builder.Configuration);
    // um Add{Modulo}Module por módulo presente nesta solution
```

Isso é o que "`Api` é composition root" significa na prática: nenhum módulo se registra sozinho, nenhum módulo conhece outro módulo, e o único lugar que conhece a lista completa de módulos da solution é o `Program.cs` do `Api`.

**Grafo de referência de projeto, da base ao topo:**

```text
SharedKernel                                    nada dentro da solution é referenciado por ele
    ↑
{Modulo}.Contracts                              referencia só SharedKernel
    ↑
{Modulo} (Domain + Application + Infrastructure) referencia {Modulo}.Contracts (implementa as mensagens) + SharedKernel
    ↑
Api                                             referencia {ModuloA}.Contracts, {ModuloB}.Contracts, ..., SharedKernel
                                                 nunca o interior de nenhum {Modulo}
```

**Exemplo de solution com dois módulos**, usando os nomes que aparecem em outros documentos desta referência (`Academico`, `Identidade`):

```text
MeuSistema.sln
├── src/
│   ├── SharedKernel/
│   │   ├── Result.cs
│   │   ├── DomainException.cs
│   │   ├── ICurrentUser.cs
│   │   └── Behaviors/ (LoggingBehavior, ValidationBehavior, AuthorizationBehavior, IdempotencyBehavior)
│   │
│   ├── Academico/
│   │   ├── Academico.Contracts/
│   │   │   └── MatricularAlunoCommand.cs
│   │   └── Academico/                  (Domain + Application + Infrastructure fundidos)
│   │       ├── Domain/MatriculaPeriodo.cs
│   │       ├── Application/MatricularAlunoCommandHandler.cs
│   │       ├── Infrastructure/AcademicoDbContext.cs
│   │       └── AcademicoDependencyInjection.cs
│   │
│   ├── Identidade/
│   │   ├── Identidade.Contracts/
│   │   │   └── CriarUsuarioCommand.cs
│   │   └── Identidade/
│   │       ├── Domain/User.cs
│   │       ├── Application/CriarUsuarioCommandHandler.cs
│   │       ├── Infrastructure/IdentidadeDbContext.cs
│   │       └── IdentidadeDependencyInjection.cs
│   │
│   └── Api/
│       ├── Program.cs
│       ├── Controllers/
│       └── Composicao/          (junta Query de mais de um módulo, ver 04-comunicacao-entre-modulos.md)
│
└── tests/
    ├── Academico.Domain.UnitTests/
    ├── Academico.Application.UnitTests/
    ├── Identidade.Domain.UnitTests/
    ├── Identidade.Application.UnitTests/
    ├── Infrastructure.IntegrationTests/     (cobre os dois módulos, ver 13-estrategia-de-testes.md)
    └── Api.IntegrationTests/
```

Cada módulo é o mesmo par `{Modulo}`/`{Modulo}.Contracts` repetido lado a lado — acrescentar um terceiro módulo é acrescentar mais uma pasta nesse mesmo padrão, nunca mexer nas duas que já existem.

## Regra de dependência: Ports & Adapters

O grafo acima não é só um fato de organização de projeto: é a aplicação de uma regra que vale em toda a solution.

**Regra direta: a dependência aponta sempre para dentro.** `Domain` não depende de nada; `Application` depende de `Domain` e de abstrações; `Infrastructure` e `Api` dependem das camadas internas, nunca o contrário. Nenhuma camada interna conhece a externa.

**Regra direta: a abstração pertence a quem a consome (a porta), a implementação a quem fornece o recurso (o adapter).** A interface de um repositório, de um serviço externo ou de um relógio é declarada na `Application`/`Domain` que a usa, não na `Infrastructure` que a implementa; a `Infrastructure` fornece o adapter concreto, registrado na composição raiz. É o oposto do instinto de pôr a interface junto da classe que a implementa — seguir esse instinto inverteria a seta de dependência.

Caveat de onde a porta mora: quando o consumidor é *qualquer* módulo mais a composição raiz (não um módulo específico), a porta sobe para o `SharedKernel`. É por isso que `ICurrentUser`, `IUnitOfWork` e os marcadores de behavior vivem lá (ver "SharedKernel", acima) — não por serem implementação, mas por serem porta compartilhada.

**Regra direta: dependência entra por injeção de construtor, nunca por service locator.** Nenhuma classe resolve a própria dependência a partir de um `IServiceProvider` passado adiante nem de um singleton estático. Uma checagem que precisa de um serviço o recebe explicitamente na assinatura (é por isso que `IRequiresAuthorization.IsAuthorizedAsync` recebe um `IAuthorizationContext` por parâmetro em vez de um `IServiceProvider`, ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)).

Instâncias concretas desta regra já aparecem em [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) (`IServicoExterno` na Application, adapter na Infrastructure), [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md) (`IUnitOfWork`) e [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) (`ICurrentUser`). A direção de dependência é verificável em CI como fitness function (ver [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md)).

## Versão de pacotes: Central Package Management

Com vários projetos por módulo repetidos lado a lado, a versão de cada dependência (EF Core, Mediator, FluentValidation, resiliência) deve ser declarada uma vez só, via **Central Package Management** (`Directory.Packages.props` na raiz da solution, com `<PackageVersion>`), nunca versão por `.csproj`. É o mesmo espírito da fronteira fixa: o que precisa ser idêntico em todo módulo fica declarado num lugar único, em vez de replicado e sujeito a divergir. Um módulo novo herda as versões centrais só por referenciar o pacote, sem repetir o número.

## Responsabilidade de cada camada

| Camada | Responsabilidade | Nunca faz |
|---|---|---|
| `Api` | Controller fino: traduz requisição em `Command`/`Query` via `ISender`, e o resultado em resposta HTTP. Composition root (seção anterior). Papel de BFF. | Regra de negócio, acesso a `DbContext`, autorização dependente de dado (isso é `Application`) |
| `Application` (dentro de `{Modulo}`) | `Command`/`Query`/handler (Mediator/CQRS), pipeline behaviors específicos do módulo (se houver), tradução de exceção de domínio para `Result.Failure` | Regra de negócio do agregado, mapeamento EF Core |
| `Domain` (dentro de `{Modulo}`) | Agregados, invariante de negócio, eventos de domínio | Autorização (nunca mora no agregado), acesso a configuração, dependência de `HttpContext`/EF Core |
| `Infrastructure` (dentro de `{Modulo}`) | `DbContext`, `IEntityTypeConfiguration`, repositórios, interceptor de auditoria | Regra de negócio, exposição fora do módulo além de `Contracts` |
| `{Modulo}.Contracts` | Única superfície pública do módulo: `Command`/`Query`/DTO que `Api` ou outro módulo estão autorizados a conhecer | Entidade de domínio, `DbContext`, handler |
| `SharedKernel` | `Result<T>`, `DomainException`, marcadores de pipeline behavior, as implementações genéricas de behavior | Qualquer conceito de negócio de um módulo específico |

No ponto de partida de 2 projetos, `Application`, `Domain` e `Infrastructure` vivem fundidos no mesmo assembly `{Modulo}` — a responsabilidade de cada um não muda com a fusão, só a fronteira física entre eles, que passa a ser pasta em vez de projeto.

**Consequência para onde o `Command`/`Query` mora:** como `Api` só pode referenciar `Contracts` (nunca o interior do módulo), todo `Command`/`Query` que tem endpoint HTTP precisa ser um tipo público em `Contracts`, não um tipo interno em `Application`. Isso não contradiz `Application` ser dona do Mediator/CQRS: o *handler* continua `internal`, dentro de `Application`, porque o Mediator o descobre por reflection/DI dentro do próprio assembly — quem chama de fora nunca instancia o handler diretamente, só envia a mensagem pública. `Contracts` acumula, então, tanto o que outro módulo pode consumir quanto o que `Api` precisa montar a partir de uma requisição HTTP.

## Exemplo mínimo por camada

```csharp
// {Modulo}.Contracts — tipo de mensagem público; Result<T> vem do SharedKernel
public sealed record CriarPedidoCommand(Guid ClienteId, decimal Valor) : IRequest<Result<Guid>>;

// {Modulo}/Application — handler interno, descoberto por DI, nunca chamado direto de fora
internal sealed class CriarPedidoCommandHandler : IRequestHandler<CriarPedidoCommand, Result<Guid>>
{
    // ValueTask: assinatura do Mediator source-generated (ver "Mediador", abaixo).
    // AdicionarAsync encena a escrita; o commit é único, no UnitOfWorkBehavior (ver 25).
    public async ValueTask<Result<Guid>> Handle(CriarPedidoCommand cmd, CancellationToken ct)
    {
        var pedido = Pedido.Criar(cmd.ClienteId, cmd.Valor); // regra de negócio no agregado
        await _repositorio.AdicionarAsync(pedido, ct);
        return Result.Success(pedido.Id);
    }
}

// {Modulo}/Domain — agregado interno; DomainException (base) vem do SharedKernel
internal sealed class Pedido
{
    public static Pedido Criar(Guid clienteId, decimal valor) =>
        valor > 0 ? new Pedido(clienteId, valor) : throw new DomainException("Valor deve ser positivo.");
}

// {Modulo}/Infrastructure — mapeamento e acesso a dado, interno
internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido> { /* ... */ }

// Api — controller fino, só traduz HTTP em Command e Result em resposta
[HttpPost]
public async Task<IActionResult> CriarPedido(CriarPedidoCommand command, ISender sender) =>
    (await sender.Send(command)).ToActionResult();
```

Cada camada aparece com o mínimo necessário pra existir: o tipo de mensagem, o handler que a resolve, o agregado que decide a regra, o mapeamento que persiste, e o controller que só traduz — nenhuma linha de regra de negócio fora do `Domain`, nenhuma linha de HTTP fora do `Api`.

## Por que regra objetiva, e não julgamento caso a caso

Quando este padrão se replica entre times diferentes, depender de julgamento ad hoc por sistema tende a gerar estruturas inconsistentes, sem ganho real sobre uma regra escrita. A parte que precisa ser idêntica em todo lugar é a fronteira fixa (seção anterior), não a contagem de projetos internos.

## Sobre um template de scaffold reutilizável

**Não recomendado como ponto de partida.** Distribuir um template `dotnet new` (equivalente interno ao que Ardalis publica como OSS) já com a separação `Contracts`, `internal` por padrão e o projeto de teste arquitetural plugados até reduziria a chance de a fronteira fixa ser reimplementada de memória, de forma divergente, a partir deste documento. Mas um template exige dono, versionamento e atualização contínua; sem isso, ele defasa e vira fonte de inconsistência pior do que não ter template nenhum, e esse custo de manutenção contínua supera, hoje, o ganho de partir de um scaffold pronto. Este documento, seguido à risca, já é suficiente para replicar a fronteira fixa manualmente. Decisão revisável: se a divergência entre times na prática mostrar que o documento sozinho não basta, vale reabrir a discussão do template com um dono definido.

## O que este documento não decide

- Onde e como uma adoção formal deste padrão é registrada (ADR ou outro formato), por sistema.
- Quem tem autoridade para aceitar esta recomendação para um sistema específico.
- Se o template de scaffold vira responsabilidade de alguém, e de quem.

## Nota de aplicação

Um sistema concreto pode avaliar esta recomendação e divergir conscientemente em pontos como estes, por decisão explícita de quem conduz aquela arquitetura, não por desconhecimento desta regra:

- Manter 4 projetos fixos por módulo (`Domain`, `Application`, `Infrastructure`, `Contracts`) para todos os módulos, em vez do ponto de partida enxuto de 2 projetos com promoção por sinal; por exemplo, quando a organização prefere uniformidade previsível entre módulos a economia inicial de estrutura.
- Deixar o teste arquitetural de fronteira entre módulos para entrar depois, quando houver mais de um módulo implementado, em vez de nascer junto com o primeiro.

Um sistema pode divergir desta referência corporativa; o registro dessa divergência, com o motivo, é o que evita que a próxima pessoa a ler aquela arquitetura confunda uma decisão consciente com uma inconsistência não percebida.
