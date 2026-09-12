# Comunicação entre módulos: leitura e escrita

## Por que a regra de bounded context vale mesmo dentro do mesmo processo

Num monólito modular, cada módulo é um bounded context no sentido pleno do termo: fronteira de dados própria, agregados próprios, só acessado por outro módulo através do que expõe explicitamente. A diferença para bounded contexts em deployables separados é só de deployment, não de fronteira de dados. A regra de que "uma consulta que precisa juntar dados de mais de um bounded context nunca deveria consultar o `DbContext`/repositório de outro context diretamente" não existe por causa da distância de rede entre dois contexts. Existe para proteger o ownership dos dados: quem pode mudar o schema, quem decide o significado de uma coluna. Um único processo e um único banco físico não mudam esse motivo.

**Regra direta: nenhum módulo referencia o `DbSet`, a `IEntityTypeConfiguration` ou o repositório de outro módulo. A única superfície pública de um módulo é o projeto `{Modulo}.Contracts`** (ver [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)).

**Regra direta: o grafo de dependência entre módulos é acíclico.** Passar por `Contracts` não basta se as duas direções existem: A referenciar `B.Contracts` **e** B referenciar `A.Contracts` compila, passa no teste de fronteira, e mesmo assim colapsa os dois módulos num só de fato — eles não podem mais ser entendidos, versionados nem extraídos separadamente. Se A e B parecem precisar se conhecer mutuamente, ou um evento inverte uma das direções (ver "Escrita: orquestração entre módulos", abaixo), ou os dois conceitos pertencem ao mesmo módulo (ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md)). A aciclicidade é uma fitness function distinta da fronteira, verificada em CI (ver [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md)).

## Escrita: orquestração entre módulos

Um fluxo de negócio real atravessa módulos com frequência: um evento no módulo A deveria disparar uma reação no módulo B.

**Regra direta: o módulo que emite o evento nunca chama o módulo que reage por injeção direta.** O mecanismo é o dispatch in-process (pós-commit) já usado dentro de um único bounded context: um `INotificationHandler` no módulo que **reage**, registrado só ali, despachando um novo `Command` via `ISender` contra o `Contracts` do módulo dono:

```csharp
// ModuloB.Application/EventHandlers/ReagirAoEventoDoModuloAHandler.cs
public sealed class ReagirAoEventoDoModuloAHandler
    : INotificationHandler<DomainEventNotification<EventoDoModuloA>>
{
    private readonly ISender _sender;

    public async ValueTask Handle(
        DomainEventNotification<EventoDoModuloA> notification,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new ComandoDoModuloB(notification.DomainEvent.IdRelevante), cancellationToken);
    }
}
```

`ComandoDoModuloB` vive em `ModuloB.Contracts`: é o mesmo tipo que o próprio módulo B usaria internamente, e é o que o módulo A está autorizado a conhecer dele. Isso **é** uma chamada a serviço, hoje in-process via `ISender`, sem HTTP nem gRPC porque os módulos compartilham o mesmo processo. O contrato (o `Command` em `Contracts`) já é a superfície que, se um módulo for extraído para um deployable próprio no futuro, vira chamada de API/gRPC real sem que o módulo chamador mude. Só a implementação de "como despachar" muda, não o que é despachado.

**Onde o handler reativo mora:** na pasta do módulo que reage, nunca no módulo que emite. Um módulo nunca referencia outro, nem em código nem em pacote, além de `Contracts`.

**Nota: mudar a forma de um tipo em `Contracts` não é o mesmo problema que versionar API externa.** [`18-versionamento-de-api.md`](18-versionamento-de-api.md) resolve breaking change para um cliente HTTP externo, que pode estar rodando uma versão mais antiga do contrato enquanto o servidor já mudou. Dentro da mesma solution, `{Modulo}.Contracts` não tem esse problema: todo consumidor compila junto, no mesmo commit, no mesmo deploy — uma mudança que quebra a assinatura de um `Command`/`Query` já usado por outro módulo é erro de compilação, não incompatibilidade em produção. Não é necessário versionamento de `Contracts` interno; é necessário só que a mudança e todos os consumidores afetados façam parte do mesmo Pull Request.

**Consequência a aceitar:** consistência eventual dentro do próprio processo, mesmo quando os dois módulos vivem no mesmo binário. Aceitável para efeitos que o usuário não precisa ver na mesma resposta HTTP. Se a ausência imediata quebra algo que o usuário vê na mesma requisição, o problema é de fronteira de módulo, não de orquestração (revisar se os dois conceitos deveriam estar no mesmo módulo, ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md)).

**Nota: dispatch in-process não é o único mecanismo possível aqui.** Este documento assume dispatch in-process como default para orquestração entre módulos, mas há gatilhos concretos em que a orquestração deveria usar Outbox em vez de dispatch in-process: mais de um módulo reagindo ao mesmo evento (fan-out real), ou quando a garantia "melhor esforço, log e segue" não é suficiente para aquele fluxo. Ver critério completo em [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md).

### Autorização quando quem despacha é um handler, não um usuário

`ComandoDoModuloB`, no exemplo acima, pode implementar `IRequiresAuthorization` (ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)) porque, quando despachado pelo `Api` em nome de um usuário, ele precisa dessa checagem. Mas `ReagirAoEventoDoModuloAHandler` roda dentro do mesmo escopo de injeção de dependência da requisição original — se ela existir —, então `ICurrentUser` ali dentro **não está vazio, resolve para o mesmo usuário que disparou o módulo A**. Isso é o problema: a checagem `IsAuthorizedAsync` do módulo B avaliaria se aquele usuário tem vínculo com o recurso do módulo B, quando na verdade quem está agindo é o sistema reagindo a um evento, não o usuário pedindo aquela ação específica.

**Regra direta: um handler reativo nunca deixa o `ICurrentUser` da requisição original vazar para dentro do `Command` que ele mesmo dispara.** Ele assume, de forma explícita, uma identidade de ator automatizado — o mesmo conceito de identidade técnica já estabelecido em [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) e [`21-auditoria.md`](21-auditoria.md), aplicado aqui ao dispatch dentro do próprio processo, não só à chamada a sistema externo:

```csharp
// Definição canônica em 11-autenticacao-e-sessao.md; aqui só os membros usados por este fluxo.
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SessionId { get; }
    bool IsAuthenticated { get; }
    bool IsSystemActor { get; } // true só para a identidade de sistema abaixo
    bool IsInRole(string role);
}

public interface ICurrentUserAccessor
{
    ICurrentUser Current { get; }

    // Troca o ICurrentUser ambiente por uma identidade de sistema bem conhecida,
    // pelo tempo de vida do IDisposable devolvido. UserId fixo, IsSystemActor = true.
    IDisposable BeginSystemIdentity(string nomeDoProcesso);
}

public sealed class ReagirAoEventoDoModuloAHandler
    : INotificationHandler<DomainEventNotification<EventoDoModuloA>>
{
    private readonly ISender _sender;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public async ValueTask Handle(
        DomainEventNotification<EventoDoModuloA> notification,
        CancellationToken cancellationToken)
    {
        using (_currentUserAccessor.BeginSystemIdentity(nameof(ReagirAoEventoDoModuloAHandler)))
        {
            await _sender.Send(new ComandoDoModuloB(notification.DomainEvent.IdRelevante), cancellationToken);
        }
    }
}
```

`ComandoDoModuloB.IsAuthorizedAsync` checa `currentUser.IsSystemActor` explicitamente — normalmente aceitando sempre que verdadeiro (a reação a evento já foi autorizada implicitamente quando o módulo A autorizou a ação original), mas essa é uma decisão explícita e visível do `Command`, nunca um efeito colateral de o `ICurrentUser` estar "por acaso" preenchido com o usuário errado:

```csharp
public sealed record ComandoDoModuloB(Guid IdRelevante) : IRequest<Result<Unit>>, IRequiresAuthorization
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken ct) =>
        Task.FromResult(currentUser.IsSystemActor || /* regra normal de autorização do Command */ true);
}
```

Se `IAuditable` também se aplica, o identificador do ator de sistema (não o usuário original) é o que vira `ActorUserId` do registro, mesma regra de "ator automatizado" de `21`.

## Leitura entre módulos

Uma leitura que precisa de dado de outro módulo consulta o módulo dono, nunca o schema do módulo dono diretamente; o mecanismo concreto varia conforme o cenário:

| Cenário | Mecanismo |
| --- | --- |
| Tela/caso de uso precisa de dado de **um** módulo alheio, pontualmente | `Query` do `Contracts` daquele módulo, despachada via `ISender` (ver "Leitura pontual", abaixo) |
| Tela precisa juntar dado de **poucos** módulos, baixo volume/frequência, composição simples | Orquestração na borda (BFF/Api), ver "Composição na borda", abaixo |
| Composição se repete em mais de um endpoint, ou volume/frequência altos | Read model dedicado, populado de forma assíncrona (ver "Projeção via fila", abaixo) |
| SLA de leitura incompatível com consultar os módulos dono em tempo real | Mesmo read model dedicado, com armazenamento físico separado se o volume justificar |

### Leitura pontual: um handler consultando um módulo alheio

Nem toda leitura entre módulos acontece na borda (Api). Um caso de uso do próprio módulo às vezes precisa confirmar ou enriquecer com um dado de outro módulo antes de decidir sua própria regra de negócio — a mesma `Query` do `Contracts` alheio, despachada via `ISender`, mas chamada de dentro do handler do módulo que precisa, na `Application`, nunca no `Domain`:

```csharp
// Academico.Application/Matriculas/MatricularAlunoCommandHandler.cs
internal sealed class MatricularAlunoCommandHandler : IRequestHandler<MatricularAlunoCommand, Result<Guid>>
{
    private readonly ISender _sender;
    private readonly IMatriculaPeriodoRepository _repositorio;

    public async ValueTask<Result<Guid>> Handle(MatricularAlunoCommand command, CancellationToken ct)
    {
        // Query pontual a um módulo alheio: só confirma que o usuário existe, não decide nada por ele
        var usuario = await _sender.Send(new ConsultarUsuarioPorIdQuery(command.UsuarioId), ct);
        if (usuario.IsFailure)
            return Result.Failure("Usuário não encontrado.");

        var matricula = MatriculaPeriodo.Criar(command.UsuarioId, command.PeriodoLetivoId);
        await _repositorio.AdicionarAsync(matricula, ct);
        return Result.Success(matricula.Id);
    }
}
```

`ConsultarUsuarioPorIdQuery` vem de `Identidade.Contracts` — o handler de `Academico` trata `Identidade` exatamente como o `Api` trataria: só através do `Contracts`, nunca do schema ou do agregado interno. **O `Domain` nunca faz essa chamada.** Se `MatriculaPeriodo.Criar(...)` precisasse saber algo sobre o usuário, esse dado já chegaria resolvido como parâmetro do método de negócio — o agregado não sabe que `Identidade` existe.

### Composição na borda: BFF/Api chamando mais de um módulo

Nem toda tela que junta dado de mais de um módulo precisa de read model dedicado desde o primeiro dia. Uma tela de resumo pode ser resolvida despachando uma `Query` para cada módulo dono e compondo a resposta na camada de composição raiz (BFF/Api, ver [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md)).

**Regra direta: a composição mora numa classe própria da camada Api, nunca inline dentro da action do controller.** Isso mantém o controller fino (traduz requisição em Command/Query e resultado em resposta) e dá um lugar único para testar a composição.

**Regra direta: a classe de composição nunca contém regra de negócio própria.** Só chama as `Query`s dos módulos e formata o resultado. Qualquer decisão sobre o que priorizar, calcular ou validar entre os dados de dois módulos é sinal de que a composição deixou de ser composição e virou um caso de uso novo, que precisa de dono (um módulo) e de agregado/regra próprios.

**Regra direta: falha parcial não é o padrão silencioso.** Se uma das `Query`s falhar, a composição falha inteira por padrão: comportamento previsível, com o mesmo contrato de erro de [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md). Resposta parcial só é aceitável quando o endpoint documenta isso explicitamente e o cliente sabe tratar o campo ausente.

**Exemplo mínimo**, uma tela que junta dado de dois módulos (mora em `Api/Composicao/`, ver [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)):

```csharp
// Api/Composicao/ResumoAlunoComposicao.cs
public sealed class ResumoAlunoComposicao
{
    private readonly ISender _sender;

    public async Task<Result<ResumoAlunoDto>> ObterAsync(Guid alunoId, CancellationToken ct)
    {
        // Cada Query pertence ao Contracts do módulo dono; a composição só chama e junta.
        var matriculas = await _sender.Send(new ConsultarMatriculasDoAlunoQuery(alunoId), ct);
        var usuario = await _sender.Send(new ConsultarUsuarioPorAlunoIdQuery(alunoId), ct);

        if (matriculas.IsFailure || usuario.IsFailure)
            return Result.Failure("Não foi possível montar o resumo do aluno."); // falha parcial não é o padrão

        return Result.Success(new ResumoAlunoDto(matriculas.Value, usuario.Value));
    }
}

// Api/Controllers/AlunosController.cs — controller continua fino, só delega
[HttpGet("{alunoId}/resumo")]
public async Task<IActionResult> ObterResumo(Guid alunoId, ResumoAlunoComposicao composicao) =>
    (await composicao.ObterAsync(alunoId, HttpContext.RequestAborted)).ToActionResult();
```

`ConsultarMatriculasDoAlunoQuery` vem de `Academico.Contracts`, `ConsultarUsuarioPorAlunoIdQuery` de `Identidade.Contracts` — a composição referencia os dois, nunca o interior de nenhum módulo. Nenhuma regra de negócio aparece aqui: se surgir a tentação de decidir algo (ex.: priorizar um dado sobre outro), é sinal de que isso deixou de ser composição e virou caso de uso novo, precisando de dono.

**Sinais de que a composição na borda deveria virar read model dedicado:**

- Mais de dois módulos sendo consultados na mesma composição.
- A mesma composição, ou muito parecida, aparece em mais de um endpoint.
- Medição real mostra que o tempo de resposta é dominado pela soma das chamadas aos módulos, não pelo processamento em si.
- A composição volta a acumular regra de negócio própria, sinal de que precisa de um caso de uso com dono, não de mais orquestração na borda.

**Critério objetivo de exemplo, para decidir sem depender só de julgamento qualitativo:** latência P95 da composição acima de 300ms atribuída à soma das chamadas aos módulos, ou mais de dois módulos consultados na mesma composição. Este é um critério de partida, não um número definitivo: cada sistema concreto que adotar esta referência deve calibrá-lo para o seu próprio SLA de leitura e ajustar o limiar conforme a experiência real de produção.

**Nota: este critério é independente do critério de promoção de módulo para 4 projetos** (ver [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)). Os dois usam sinais objetivos e limiares para decidir "quando vale o passo seguinte", mas resolvem problemas diferentes: read model dedicado é sobre escalabilidade de **leitura entre módulos**; promoção para 4 projetos é sobre **granularidade interna** de um único módulo. Um sistema pode precisar de um sem o outro, ou dos dois ao mesmo tempo, sem relação de causa entre eles.

### Projeção via fila, para CQRS mais completo

Quando um read model precisa se manter sincronizado com dado de outro módulo, ele **não** é populado por consulta cross-schema no momento da leitura. É populado de forma assíncrona, reaproveitando a infraestrutura de Outbox já adotada para saída de processo (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)): o módulo dono publica um evento de integração via Outbox; um worker consome e atualiza a tabela de read model do módulo consumidor. Isso não introduz um broker novo por padrão: reaproveita a infraestrutura de Outbox e worker já existente, no mesmo espírito de "broker só quando o Outbox se mostrar insuficiente".

**Exemplo mínimo**, a cópia denormalizada mora na `Infrastructure` do módulo consumidor, nunca lê o schema do módulo dono:

```csharp
// Academico.Infrastructure/Projections/AtualizarNomeDoUsuarioNaMatriculaHandler.cs
internal sealed class AtualizarNomeDoUsuarioNaMatriculaHandler
    : INotificationHandler<IntegrationEventNotification<UsuarioRenomeadoIntegrationEvent>>
{
    private readonly AcademicoDbContext _dbContext;

    public async ValueTask Handle(
        IntegrationEventNotification<UsuarioRenomeadoIntegrationEvent> notification,
        CancellationToken ct)
    {
        // Atualiza a cópia denormalizada no próprio schema de Academico; nunca lê o schema de Identidade
        await _dbContext.MatriculaResumo
            .Where(m => m.UsuarioId == notification.IntegrationEvent.UsuarioId)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.NomeUsuario, notification.IntegrationEvent.NomeNovo), ct);
    }
}
```

`UsuarioRenomeadoIntegrationEvent` é publicado por `Identidade` via Outbox; o worker entrega para este handler, que vive dentro de `Academico` (o consumidor), nunca dentro de `Identidade` (o dono do dado original). A tabela `MatriculaResumo` é schema de `Academico`, com uma coluna `NomeUsuario` que é cópia, não fonte de verdade — a fonte continua sendo `Identidade`.

## Exceção: consulta direta multi-schema

**Regra direta, restritiva:** uma consulta que acessa o schema de mais de um módulo diretamente só é aceitável como exceção pontual, e exige aprovação explícita em revisão de arquitetura antes de ser mesclada. Não é uma opção de mesmo nível que "`Query` do `Contracts`" ou "read model". Candidato razoável a pedir essa exceção: um relatório muito distante do modelo de escrita, de baixo volume, onde criar um read model dedicado ainda não se paga. Toda exceção aprovada deve ficar registrada (comentário no código apontando para a aprovação, ou ADR equivalente), para não virar precedente informal para a próxima consulta parecida.

## Veja também

- [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md): `Contracts` como superfície pública do módulo
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): Outbox e worker, reaproveitados aqui para popular read model entre módulos
- [`03-commands-e-queries.md`](03-commands-e-queries.md): mecanismo de despacho de Command/Query usado tanto dentro quanto entre módulos
- [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md): papel de BFF/Api, onde a composição na borda vive
- [`29-modelagem-e-dados-entre-modulos.md`](29-modelagem-e-dados-entre-modulos.md): a camada de modelagem por baixo destes mecanismos — referência vs. snapshot vs. cópia, invariante na fronteira, integridade sem FK, ACL interno
