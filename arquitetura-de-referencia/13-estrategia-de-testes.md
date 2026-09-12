# Estratégia de testes por módulo

## Um projeto de teste por camada, por módulo

Cada projeto testa só a camada correspondente, nunca mais de uma ao mesmo tempo.

| Projeto | O que exercita | Com o quê |
| --- | --- | --- |
| `{Modulo}.Domain.UnitTests` | Regras de negócio do agregado do módulo | xUnit puro, sem EF Core, sem Mediator, sem banco |
| `{Modulo}.Application.UnitTests` | Handlers de command/query do módulo, incluindo `IRequiresAuthorization` (ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)) | Fakes/mocks das interfaces do módulo; `ICurrentUser` fake para os cenários de autorização por recurso |
| `Infrastructure.IntegrationTests` | Mapeamento EF Core de todos os módulos, repositórios, conflito de concorrência | Banco real efêmero (Testcontainers) |
| `Api.IntegrationTests` | Pipeline HTTP completo: controllers, autenticação/sessão, `GlobalExceptionHandler`, pipeline behaviors, `/health` | `WebApplicationFactory<Program>`; adapters de integração externa (ver [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md)) substituídos por fake |

O teste de `{Modulo}.Application.UnitTests` para um handler cobre dois aspectos distintos, não um só, e cada um com seu próprio fake: a checagem de `IRequiresAuthorization` (com um `ICurrentUser` fake representando o usuário autorizado ou não) e a regra de negócio do próprio handler (com um fake do repositório/agregado envolvido), tipicamente em métodos de teste separados. Os dois aspectos não se confundem porque não moram no mesmo lugar conceitual: como [`02-dominio-hibrido.md`](02-dominio-hibrido.md) explica, autorização nunca mora no agregado — ela só faz sentido quando existe um usuário logado pedindo algo, então não é testada como invariante de domínio no `{Modulo}.Domain.UnitTests`, e sim como parte do handler na Application, onde o `ICurrentUser` está disponível.

**Aceitável, não obrigatório**: um único projeto de infraestrutura/API cobrindo todos os módulos (em vez de um por módulo), quando o `DbContext` também é único (ver [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)), já que não há motivo para duplicar o setup de Testcontainers/`WebApplicationFactory` por módulo. `Domain` e `Application` continuam separados por módulo, pois são eles que precisam ficar isolados uns dos outros.

## Fitness functions arquiteturais

**Regra direta: toda regra arquitetural que pode virar teste automatizado vira teste — não fica só como convenção de code review.** Uma fitness function (`ArchUnitNET`, `NetArchTest`, ou verificação de referências de assembly) codifica uma regra estrutural e falha o build quando ela é violada. É o que transforma os documentos desta referência de "convenção que se espera" em "invariante verificado", e o motivo pelo qual essa suíte nasce junto com o primeiro módulo, não depois (ver [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md), "Hierarquia de garantia" e _boundary theater_).

Catálogo mínimo, do mais crítico ao menos:

| Fitness function | O que verifica | Regra de origem |
| --- | --- | --- |
| Fronteira de módulo | Nenhum projeto de um módulo referencia o interior de outro além do `Contracts` dele; `Api` idem | [`04`](04-comunicacao-entre-modulos.md), [`01`](01-estrutura-de-projetos-monolito-modular.md) |
| Dependências acíclicas | O grafo de dependência entre módulos é um DAG: não existe A→B e B→A, nem via `Contracts` | [`04`](04-comunicacao-entre-modulos.md) (ver nota) |
| Direção de dependência | A seta aponta para dentro: `Domain` não referencia `Application`/`Infrastructure`; nada interno referencia `Api` | [`01`](01-estrutura-de-projetos-monolito-modular.md), Ports & Adapters |
| Pureza do `Domain` | `Domain` não referencia EF Core/`DbContext` nem `HttpContext`, e não lê relógio/aleatoriedade (`DateTime.Now`) | [`01`](01-estrutura-de-projetos-monolito-modular.md), [`10`](10-configuracao-e-segredos.md), seção de tempo abaixo |
| `internal` fora de `Contracts` | Todo tipo fora de `{Modulo}.Contracts` é de fato `internal` | [`01`](01-estrutura-de-projetos-monolito-modular.md) |
| Convenção de nome | `Command`/`Query`/`Handler`/`Validator` seguem o sufixo padrão | [`08`](08-convencao-de-nomenclatura.md) |

**Nota sobre aciclicidade:** o teste de fronteira sozinho **não** pega um ciclo — A referenciando `B.Contracts` e B referenciando `A.Contracts` compila e passa na fronteira, mas colapsa os dois módulos num só de fato (não podem mais ser entendidos, versionados ou extraídos separadamente). É a forma mais silenciosa de erosão de fronteira, e por isso a aciclicidade é uma fitness function distinta, não um caso do teste de fronteira.

## Casos que merecem teste dedicado

- **`IdempotencyBehavior`**: segunda chamada com a mesma chave após sucesso não executa o handler de negócio de novo; uma exceção transitória vinda do `IUnitOfWork` fake propaga através do behavior sem que a operação seja marcada como concluída (ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)).
- **Serialização de `Result<T>`**: serializar e desserializar um `Result<T>` de verdade, para não reintroduzir silenciosamente um problema de construtor privado sem `[JsonConstructor]`.
- **Leitura rastreada vs. não rastreada**: teste de infraestrutura confirmando qual método de repositório rastreia a entidade e qual não.
- **Conflito de concorrência de fato**: carregar o mesmo agregado em dois `DbContext`s separados, confirmar e salvar em um, depois tentar salvar a alteração pendente no outro e verificar que a exceção própria da Application é lançada.
- **Orquestração reativa entre módulos** (evento de um módulo dispara `Command` em outro, ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)): não tem dono num único módulo, porque nenhum `Application.UnitTests` isolado tem os handlers reais dos dois módulos coexistindo (o próprio ponto do teste unitário é isolar com fake). Esse cenário é exercido em `Api.IntegrationTests`, o único projeto que registra todos os módulos reais na mesma composição raiz — confirma que o evento do módulo A realmente dispara o `Command` do módulo B, e que a identidade de ator automatizado (não o `ICurrentUser` da requisição original) é o que chega no módulo B.

## Testabilidade de tempo e dependências implícitas

**Regra direta: o `Domain` nunca lê relógio, aleatoriedade ou qualquer outro estado ambiente por conta própria, e sim recebe como parâmetro explícito.** Quem resolve isso é a `Application`, através de `TimeProvider` injetado no handler (`TimeProvider.System` em produção, um provider fake nos testes de handler). O agregado nunca injeta `TimeProvider` nem qualquer outra dependência de infraestrutura: ele recebe o valor de tempo já resolvido (um `DateTimeOffset`, por exemplo) como parâmetro do método de negócio, passado pelo handler da Application depois de consultar o `TimeProvider`. O agregado continua sendo um POCO puro, testável passando qualquer valor literal, sem fake, sem mock, sem setup de DI.

## Veja também

- [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md): por que infraestrutura/Api podem ser únicos e `Domain`/`Application` são por módulo
- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): por que autorização nunca mora no agregado, e por isso é testada na Application, não no Domain
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): regra que o teste arquitetural verifica
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): cenário de teste de `IRequiresAuthorization`
