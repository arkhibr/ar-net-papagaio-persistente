# Contrato de erro HTTP, idempotência e concorrência

## `Result.Failure` vs. exceção

**Regra direta:** só falha de negócio determinística vira `Result<T>.Failure(...)`: dado o mesmo agregado e a mesma entrada, o resultado é sempre igual (ex.: `DomainException` capturada no handler). É seguro cachear essa resposta como definitiva pela idempotência. Falha transitória de infraestrutura (ex.: conflito de gravação otimista) continua sendo exceção não capturada, traduzida pelo `GlobalExceptionHandler`. Se fosse `Result.Failure`, o `IdempotencyBehavior` marcaria a operação como concluída e a devolveria para sempre sob aquela chave, mesmo que uma nova tentativa pudesse ter sucesso.

## Idempotência

**Regra direta: qualquer endpoint que dispare uma chamada a um serviço externo com efeito colateral cobrável/irreversível, ou uma transição de estado que não pode acontecer duas vezes por reenvio acidental do cliente, exige `Idempotency-Key`.** Mecanismo: chave via header HTTP, `Command` expõe `IIdempotentCommand`, `IdempotencyBehavior` no pipeline (ver [`03-commands-e-queries.md`](03-commands-e-queries.md)).

## Concorrência otimista

**Regra direta:** `RowVersion` como shadow property do EF Core protege contra duas requisições **diferentes** disputando o mesmo agregado ao mesmo tempo; `Idempotency-Key` protege contra reenvio da **mesma** requisição. São proteções complementares, não substitutas uma da outra. `UnitOfWork` é a única classe da solução que menciona `DbUpdateConcurrencyException` pelo nome, traduzindo para uma exceção própria da Application antes de propagar.

Vale ser preciso sobre o que cada proteção cobre. Se um efeito colateral externo (ex.: chamar um gateway/sistema externo) acontece antes do `SaveChanges`, uma corrida genuína entre duas requisições diferentes ainda pode disparar esse efeito duas vezes, mesmo que a segunda gravação seja depois rejeitada pelo `RowVersion`. O `RowVersion` impede a gravação duplicada, mas não desfaz um efeito colateral que já foi disparado. A defesa principal contra efeito colateral duplicado continua sendo a `Idempotency-Key`, que cobre o caso mais comum na prática: o cliente reenviando a mesma requisição.

Consequência prática: nem `RowVersion` nem `Idempotency-Key` desfazem uma chamada externa que já saiu. Os dois protegem o registro no banco (contra gravação duplicada e contra reprocessamento da mesma requisição, respectivamente), não a chamada em si. Por isso, toda dependência externa chamada **antes** do `SaveChanges` (ex.: gateway de pagamento, serviço externo) precisa ser idempotente por design, ou seja, aceitar reenvio da mesma operação sem duplicar o efeito (ex.: chave de idempotência própria do provedor externo, deduplicação por identificador de operação). Sem isso, a janela entre a chamada externa e o `SaveChanges` é um ponto de duplicação real que nenhum mecanismo descrito neste documento cobre.

## Concorrência otimista na fronteira HTTP: `ETag`/`If-Match`

O `RowVersion` descrito acima protege a gravação no servidor, mas o cliente precisa de uma forma de participar do controle de concorrência sem inventar um campo próprio no corpo. O mecanismo REST para isso é condicional por header, não um campo de versão no JSON:

- Uma resposta `GET` de um recurso versionado inclui o header `ETag` com o valor do `RowVersion` atual (opaco para o cliente).
- Uma requisição que muda estado envia `If-Match` com o `ETag` que recebeu. O servidor compara com o `RowVersion` atual: se divergem, o recurso mudou desde a leitura e a operação é rejeitada com **412 Precondition Failed** (ou 409, quando a divergência só é detectada no `SaveChanges` — ver tabela abaixo), antes de aplicar a mudança.

**Regra direta: `ETag`/`If-Match` é a superfície HTTP do mesmo `RowVersion`, não um segundo mecanismo.** O cliente nunca lê nem escreve o valor de versão no corpo; ele só devolve o `ETag` opaco que recebeu. Isso mantém o `RowVersion` como shadow property (ver [`14-filtro-de-dados.md`](14-filtro-de-dados.md) para o conceito de shadow property que não vaza para o DTO) e evita que o contrato JSON exponha um detalhe de concorrência de persistência. Endpoints sem concorrência relevante (ex.: criação, ações naturalmente idempotentes por `Idempotency-Key`) não precisam de `If-Match`.

## Ordem fixa dos pipeline behaviors

```text
Logging → Validation → Authorization → Idempotency → Caching → Handler
```

## Tabela exceção → HTTP status

O corpo da resposta de erro segue a [RFC 9457 (Problem Details for HTTP APIs)](https://www.rfc-editor.org/rfc/rfc9457): JSON padronizado com `type`, `title`, `status`, `detail` e `instance`. Cada linha da tabela abaixo mapeia para um `type`/`title` fixos, e `status` é a própria coluna "Status"; `detail`/`instance` variam por requisição. A linha "qualquer outra" usa `type` genérico e omite `detail`, para não vazar informação interna.

| Exceção | Status | Quando acontece |
| --- | --- | --- |
| `ValidationException` (FluentValidation) | 400 | Pipeline de validação rejeitou a entrada |
| `DomainException` | 400 | Anomalia: escapou do handler sem ser capturada (ver nota abaixo; o caminho esperado é virar `Result.Failure`, não chegar aqui) |
| `AuthorizationDeniedException` | 403 | Behavior de autorização por recurso rejeitou, dependente de dado do `Command` (ver nota abaixo e [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)) |
| `OperationInProgressException` | 409 | Mesma `Idempotency-Key`, operação anterior ainda não concluída |
| `ConcurrencyException` | 409 | Conflito de gravação otimista (`RowVersion`) detectado no `SaveChanges` |
| `PreconditionFailedException` | 412 | `If-Match` não bate com o `RowVersion` atual (concorrência detectada antes de gravar, ver seção `ETag`/`If-Match`) |
| qualquer outra | 500 | Erro não previsto, logado como erro, nunca exposto em detalhe |

**Implementação: o `GlobalExceptionHandler` é um `IExceptionHandler` (.NET 8+), não middleware artesanal.** O corpo de resposta é montado pelo serviço nativo de Problem Details (`AddProblemDetails()`), e cada linha da tabela vira um mapeamento exceção→`(status, type, title)` registrado num só lugar. Isso mantém o formato RFC 9457 consistente sem serialização manual por exceção.

**Nota sobre `DomainException`: dois caminhos possíveis, não contraditórios.**

1. **Capturada no handler (caminho normal/esperado).** O handler envolve a chamada ao método do agregado em `try/catch (DomainException)` e converte para `Result<T>.Failure(ex.Message)` (ver exemplo de handler em [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)). Por este caminho a exceção nunca chega ao `GlobalExceptionHandler`: o controller serializa a falha de negócio a partir do `Result`, não a partir de uma exceção HTTP. O corpo de erro pode até ter o mesmo formato (ver exemplo de corpo abaixo), mas a origem é o `Result.Failure`, não esta tabela.
2. **Escapou do handler sem ser capturada (anomalia de implementação).** Se um handler específico não envolver a chamada ao agregado em `try/catch`, a `DomainException` propaga como exceção não tratada e o `GlobalExceptionHandler` a traduz para 400 conforme a linha da tabela acima. Este não é o caminho esperado desta arquitetura: é o que a tabela cobre como rede de segurança, não como fluxo desejado.

**Nota sobre 403: dois mecanismos, não um só.** A linha `AuthorizationDeniedException` cobre só a leitura B (autorização dependente de dado do `Command`, resolvida no pipeline). A leitura A (papel genérico, ex.: `[Authorize(Roles = "Administrador")]` no controller) também produz 403, nativamente pelo framework, antes mesmo do `Command` ser montado, e não passa pelo `AuthorizationBehavior` nem pelo `GlobalExceptionHandler`. Ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) para a distinção completa entre as duas leituras.

### Exemplo de corpo de erro

**Regra direta: o membro de extensão `errors` (RFC 9457 permite extensão além dos cinco campos padrão) está sempre presente em erro 400, com um item ou vários, nunca só `detail` solto.** O cliente nunca precisa decidir, por tipo de exceção, se olha `detail` ou `errors`: olha sempre `errors[]`. `detail` continua presente como resumo textual da falha (útil para log e para exibição genérica), mas é redundante com `errors` por design, não a única fonte da mensagem.

Uma `DomainException` capturada pelo `GlobalExceptionHandler` produz um corpo como este, com item único e `pointer` nulo porque a violação não pertence a um campo específico do `Command`:

```json
{
  "type": "https://exemplo.com/erros/regra-de-negocio-violada",
  "title": "Regra de negócio violada",
  "status": 400,
  "detail": "O agregado não permite esta operação no estado atual.",
  "instance": "/api/v1/recursos/3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "errors": [
    {
      "pointer": null,
      "codigo": "estado_invalido",
      "mensagem": "O agregado não permite esta operação no estado atual."
    }
  ]
}
```

`type` e `title` são fixos por exceção (podem virar uma tabela de constantes no `GlobalExceptionHandler`); `status` vem da tabela acima; `detail`/`errors` variam por requisição; `instance` é a rota que originou o erro. Para a linha "qualquer outra" (500), `detail` e `errors` são omitidos e `title` fica genérico ("Erro interno"), para não vazar detalhe de implementação. O 500 é a única exceção à regra de `errors` sempre presente, porque não é erro de negócio para o cliente tratar campo a campo.

### Lista de erros de negócio (uma ou várias violações, mesmo formato)

RFC 9457 permite membros de extensão além dos cinco campos padrão. O membro `errors` (mesmo nome que o `ValidationProblemDetails` do ASP.NET Core já usa) carrega um array com uma entrada por violação, sempre. É isso que garante ao interceptor HTTP do cliente (ver [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md)) um único caminho de código para mapear erro 400 em erro de formulário, sem ramificar por tipo de exceção:

```json
{
  "type": "https://exemplo.com/erros/validacao",
  "title": "Um ou mais campos são inválidos",
  "status": 400,
  "errors": [
    {
      "pointer": "email",
      "codigo": "formato_invalido",
      "mensagem": "Email em formato inválido"
    },
    {
      "pointer": "quantidade",
      "codigo": "fora_da_faixa",
      "mensagem": "Quantidade deve ser maior que zero"
    }
  ]
}
```

- **`ValidationException` (sintática)**: `FluentValidation` já agrega todas as falhas de campo antes de lançar (ver [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md)), então o `errors` array nasce pronto, com um item por regra de campo violada e `pointer` apontando para o campo do `Command`.
- **`DomainException` (invariante)**: o agregado lança na primeira invariante violada, por padrão, porque o estado do agregado depois de uma invariante quebrada pode não ser seguro para continuar checando as próximas. Por isso o `errors` array normalmente tem um item só, com `pointer` nulo (a violação não é de um campo, é do agregado como um todo), mas nunca a ausência do array. Se um agregado específico precisar mesmo reportar mais de uma invariante violada ao mesmo tempo, isso é uma decisão de domínio deliberada (o método do agregado escolhe coletar em vez de lançar na primeira), não o comportamento padrão desta arquitetura; documente a exceção onde a regra do agregado é declarada. Em qualquer um dos dois casos, o formato do corpo não muda, só a quantidade de itens.

## Health checks e rate limiting

Cada camada registra a checagem que sabe fazer (cada `Infrastructure` de módulo, cada adapter de integração externa); a Api só agrega em `/health`. Rate limiting aplicado seletivamente em endpoints com efeito colateral caro ou externo, não na API inteira por padrão.

**Exemplo mínimo:** cada módulo registra sua própria checagem dentro do próprio método de extensão de `DependencyInjection`; `Api` só agrega, nunca conhece o que cada módulo verifica:

```csharp
// Academico/AcademicoDependencyInjection.cs
public static IServiceCollection AddAcademicoModule(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AcademicoDbContext>(/* ... */);
    services.AddHealthChecks()
        .AddDbContextCheck<AcademicoDbContext>(name: "academico-db");
    return services;
}

// Api/Program.cs — só agrega, não sabe o que cada módulo checou
builder.Services.AddAcademicoModule(builder.Configuration);
builder.Services.AddIdentidadeModule(builder.Configuration);
// ...
app.MapHealthChecks("/health");
```

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): onde cada behavior se encaixa no pipeline
- [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md): rotas e verbo HTTP por operação
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): `AuthorizationDeniedException` e `[Authorize(Roles = "...")]`, as duas leituras de autorização e seus caminhos para 403
