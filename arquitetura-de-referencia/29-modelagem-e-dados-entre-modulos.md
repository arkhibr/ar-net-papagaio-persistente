# Modelagem e dados entre módulos: referência, snapshot e invariante

> A camada de **modelagem** que fica por baixo do [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) e ao lado do [`02-dominio-hibrido.md`](02-dominio-hibrido.md). O doc 04 responde **por onde** o dado de outro módulo passa (`Contracts`, `Query`, dispatch, read model); este responde **de que forma** o dado e o invariante _existem_ quando cruzam a fronteira: se o dado é referência viva, snapshot congelado ou cópia sincronizada; onde um invariante pode morar; o que acontece sem foreign key entre schemas; e se um módulo traduz o vocabulário do outro. É o conjunto de regras que decide se as fronteiras de módulo se mantêm ou erodem umas às outras com o tempo.

## 1. Três formas de um módulo carregar dado de outro

Quando o módulo A precisa de um dado que o módulo B é dono, existem três formas, **com semânticas temporais diferentes**, e a escolha é de modelagem, não de conveniência.

| Forma | Quando | Semântica temporal | Mecanismo (doc 04) |
| --- | --- | --- | --- |
| **Referência por id** (query ao vivo) | A precisa do dado de B só _agora_, para decidir, e não o guarda | Sempre o valor atual de B; A não é dono | `Query` do `Contracts` de B, despachada no handler |
| **Snapshot por valor** | O dado vira parte de um _fato_ de A e não pode mudar depois | Congelado no instante do evento; A vira dono da cópia | Handler resolve via `Query` e congela no agregado |
| **Cópia sincronizada** (read model) | A exibe/consulta dado de B com frequência e quer o valor _atual_ sem query ao vivo | Atualizado por evento; B continua fonte de verdade | Projeção via Outbox ([`04`](04-comunicacao-entre-modulos.md), "Projeção via fila") |

**Regra direta: escolha pela semântica temporal do dado, não pela facilidade de implementar.** A pergunta decisiva é: _quando B mudar esse dado, o que deve acontecer com o que A guardou?_

- Deve refletir o valor novo → cópia sincronizada (ou referência viva, se A nem guarda).
- Deve permanecer como estava → snapshot. O valor deixou de ser "dado de B" e virou "fato de A".

**Regra direta: snapshot e cópia sincronizada são opostos em intenção, mesmo parecendo a mesma coisa (ambos copiam um valor de B para dentro de A). Snapshot NUNCA é atualizado por evento de B; cópia sincronizada SEMPRE é.** Confundir os dois é o erro clássico: o `MatriculaResumo.NomeUsuario` de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) é cópia sincronizada (é para exibição, deve seguir o nome atual). O `NomeProduto`/`ValorUnitario` de um item de pedido é snapshot (é o que foi comprado; reprecificar o produto amanhã não pode reescrever o pedido de ontem). Atualizar um snapshot por evento seria um bug de negócio, não uma melhoria de consistência.

### Construir o objeto rico: o handler congela o snapshot, o agregado recebe pronto

Este é o caso que mais confunde: um agregado rico que precisa de dado de outro módulo para nascer. O agregado **nunca** consulta o outro módulo (regra de [`02-dominio-hibrido.md`](02-dominio-hibrido.md) e [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)): quem resolve o dado é o handler da `Application`, que o **congela como valor** e o passa como parâmetro do método de negócio.

```csharp
// Domain (Vendas) — o preço e o nome viram FATO do item no instante da compra: snapshot, não referência viva.
internal sealed class ItemPedido
{
    public Guid ProdutoId { get; }         // referência por id ao agregado dono (em Catalogo)
    public string NomeProduto { get; }     // snapshot: nome no momento da compra
    public decimal ValorUnitario { get; }  // snapshot: preço no momento da compra

    internal ItemPedido(Guid produtoId, string nomeProduto, decimal valorUnitario)
    {
        if (valorUnitario <= 0)
            throw new DomainException("Valor unitário deve ser positivo."); // invariante de A, sobre o snapshot
        ProdutoId = produtoId;
        NomeProduto = nomeProduto;
        ValorUnitario = valorUnitario;
    }
}
```

```csharp
// Application (Vendas) — resolve o dado via Contracts do módulo dono e o congela no agregado.
public async ValueTask<Result<Guid>> Handle(AdicionarItemCommand command, CancellationToken ct)
{
    var produto = await _sender.Send(new ConsultarProdutoQuery(command.ProdutoId), ct);
    if (produto.IsFailure)
        return Result.Failure("Produto não encontrado."); // guard best-effort, não invariante (ver ponto 2)

    // Nome e preço viram snapshot dentro do Pedido: se o Catálogo reprecificar depois, este item não muda.
    pedido.AdicionarItem(produto.Value.Id, produto.Value.Nome, produto.Value.Preco);

    await _repositorio.SalvarAsync(pedido, ct);
    return Result.Success(pedido.Id);
}
```

O agregado continua sem saber que `Catalogo` existe: recebe três valores comuns (`Guid`, `string`, `decimal`), não um `ProdutoDto`. É a diferença entre "o `Pedido` depende do módulo `Catalogo`" (errado) e "o `Pedido` guarda o preço que foi comprado" (certo).

## 2. Invariante mora dentro da fronteira de consistência

**Regra direta: um invariante de agregado só pode depender do estado do próprio agregado mais os valores recebidos como parâmetro (snapshot no instante da operação). Nunca do estado _atual_ de outro módulo.**

O padrão "confirmar que existe via `Query` antes de criar" de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) (o handler consulta `ConsultarUsuarioPorIdQuery` antes de matricular) é um **guard de pré-condição best-effort, não um invariante**. Entre o "confirmei que existe" e o commit há uma corrida (TOCTOU): o usuário pode ter sido removido em Identidade nesse intervalo, e nenhuma transação cobre os dois módulos ([`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md): uma transação abarca um agregado, nunca dois módulos). O agregado nasce válido contra o snapshot que recebeu; ele **não** garante que o mundo em B continue coerente depois.

Daí o critério para uma regra que "parece" precisar de dado de outro módulo:

| A regra… | É… | Onde mora | Garantia |
| --- | --- | --- | --- |
| depende de um valor que A congelou (snapshot) | invariante de A | `Domain`, no método do agregado | forte, dentro da fronteira |
| exige que algo de B exista/seja verdade _no instante_ da operação | guard de pré-condição | `Application`, no handler, antes de montar o agregado | best-effort; aceita a corrida como risco conhecido |
| exige que o estado _atual_ de B se mantenha coerente ao longo do tempo | política entre módulos, ou fronteira errada | ver ponto 3, ou revisar o limite do módulo ([`02`](02-dominio-hibrido.md)) | eventual, nunca transacional |

**Regra direta: se a corrida do guard best-effort for inaceitável para um fluxo (o efeito de referenciar algo que sumiu é grave e irreversível), a resposta não é uma query cross-schema transacional — é snapshot (congelar o que importa) ou reserva (ponto 3), nunca esticar a transação de A para dentro de B.**

## 3. Invariante entre agregados ou módulos (o problema difícil)

**Regra direta: nenhum invariante que precise ser verdadeiro _entre_ dois agregados pode ser garantido transacionalmente quando eles estão em módulos diferentes. Escolha explicitamente uma das três estratégias — não deixe implícito.**

| Estratégia | Como | Quando | Custo |
| --- | --- | --- | --- |
| **Fundir num único agregado** | Se as duas coisas precisam mesmo ser consistentes no mesmo instante, talvez sejam o mesmo agregado ([`02`](02-dominio-hibrido.md)) | Quando cabem no mesmo módulo e a consistência imediata é requisito real | Agregado maior; só possível dentro de um módulo |
| **Eventual + detectar/compensar** | Commita cada lado; um handler reativo ([`05`](05-processamento-assincrono-e-eventos.md)) detecta a violação depois e compensa ou sinaliza | Quando uma janela de inconsistência é tolerável e a compensação é possível | Complexidade de compensação; janela visível |
| **Reserva (reservation)** | Reservar o recurso escasso numa transação curta; confirmar depois; a reserva expira se não confirmada | Recurso escasso disputado (vaga, estoque, número único) | Estado de reserva + expiração a manter |

**Regra direta: unicidade/conflito global entre agregados (ex.: "sem duas matrículas conflitantes para o mesmo aluno", "código único no sistema todo") é o caso mais tentador de resolver com uma consulta cross-schema — e é exatamente o que a exceção restritiva de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) ("consulta direta multi-schema") reserva para casos raros aprovados.** Antes de pedir essa exceção, prefira reserva (para recurso escasso) ou detecção eventual. Uma restrição de unicidade _dentro_ de um único módulo/schema, por outro lado, é um índice único comum, sem nada de especial.

## 4. Integridade referencial sem foreign key entre schemas

**Regra direta: como cada módulo é dono do próprio schema ([`04`](04-comunicacao-entre-modulos.md)), não existe foreign key entre módulos. A integridade de uma referência por id entre módulos é responsabilidade da aplicação, nunca do banco.** Uma FK cross-schema reintroduziria o acoplamento de ownership que a fronteira existe para impedir.

Consequência: uma referência pendente (A aponta por id para algo que B apagou) é fisicamente possível. Isso não é bug a evitar com trigger — é uma decisão de modelagem a tomar conscientemente, escolhendo uma política:

| Política | Como | Quando |
| --- | --- | --- |
| **Referência pendente tolerada** | A trata "não encontrado" na leitura como estado válido (dado exibe "—", ou o fluxo degrada) | Quando o histórico de A faz sentido mesmo sem o alvo em B (reforça o caso de snapshot: A já congelou o que precisava) |
| **B nunca apaga de fato** | B usa soft delete / inativação; a linha continua existindo, referências continuam resolvendo | Quando referências precisam sempre resolver e o volume tolera retenção |
| **Eliminação em B orquestra limpeza em A** | Apagar em B publica evento; A reage limpando/anonimizando a própria referência ([`05`](05-processamento-assincrono-e-eventos.md)) | Quando a referência órfã é inaceitável — é o mesmo mecanismo que [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md) já usa para eliminação de dado pessoal, generalizado |

**Regra direta: quem manda no ciclo de vida da referência é o módulo dono (B); A reage a eventos de B, nunca o contrário, e nunca consulta o schema de B no momento de apagar.** Se A precisasse impedir B de apagar algo (porque A ainda referencia), isso é acoplamento invertido — modele como snapshot (A não depende mais do alvo) ou como orquestração explícita, não como um veto de A sobre o schema de B.

## 5. Tradução entre módulos: ACL interno e conceito compartilhado

### O tipo de `Contracts` de B não entra no `Domain` de A

**Regra direta: um módulo não deixa o tipo de `Contracts` de outro módulo vazar para dentro do próprio `Domain`. O DTO recebido é traduzido para o vocabulário de A na fronteira (`Application`), como uma Anti-Corruption Layer ([`07-integracao-legado-acl.md`](07-integracao-legado-acl.md)), só que mais leve.** O agregado de A nunca tem um campo do tipo `Identidade.Contracts.UsuarioDto`; ele tem o próprio valor/Value Object, montado a partir daquele DTO no handler (é o que o ponto 1 já faz ao passar `Guid`/`string`/`decimal`, não o DTO inteiro).

**Trade-off explícito:** para leitura pura de exibição — composição na borda ([`04`](04-comunicacao-entre-modulos.md)) ou uma `Query` cujo resultado só vai para a tela ([`27-leitura-e-query-side.md`](27-leitura-e-query-side.md)) — usar o DTO de `Contracts` de B diretamente é aceitável: não há modelo de domínio de A para proteger, então traduzir seria cerimônia sem ganho. A ACL interna importa quando o dado **entra no modelo de escrita/decisão de A**; não quando só atravessa A a caminho da tela.

### O mesmo conceito do mundo real em dois módulos

**Regra direta: quando o mesmo conceito do mundo real aparece em dois módulos (ex.: a mesma pessoa é "Aluno" em `Academico` e "User" em `Identidade`), cada módulo modela só a faceta que é dona, e as duas se ligam por um id compartilhado — o id canônico do módulo dono da identidade daquele conceito.** Não existe uma entidade "Pessoa" única espalhada por dois schemas; existem duas representações, cada uma dona do seu pedaço, referenciadas por id.

Decidir quem é dono de qual faceta é decisão de **context mapping**, e deve ser registrada (ADR ou equivalente), porque é a decisão que, se ficar ambígua, faz dois módulos disputarem a mesma responsabilidade. O sinal de erro: dois módulos mantendo o mesmo atributo como fonte de verdade (ex.: os dois guardando o e-mail "oficial" da pessoa e ambos achando que mandam nele). Um é dono; o outro, se precisar do valor, usa referência viva, snapshot ou cópia sincronizada (ponto 1) — nunca uma segunda fonte de verdade.

## Nível de confiança

As cinco regras são síntese direta de DDD tático (fronteira de agregado, consistência eventual, context mapping, ACL) aplicada às fronteiras internas do monólito modular já estabelecidas nesta referência, com alta confiança nos princípios. As escolhas concretas por fluxo — qual das três formas do ponto 1, qual estratégia do ponto 3, qual política do ponto 4 — são decisões de cada sistema, guiadas pela semântica de negócio do dado, não números fixos desta referência.

## Nota de aplicação

Um sistema concreto pode divergir conscientemente, por exemplo:

- Adotar **soft delete como padrão** em módulos muito referenciados (política do ponto 4), aceitando o custo de retenção para nunca lidar com referência órfã.
- Permitir o **DTO de `Contracts` de B diretamente em read models de A** (não só na tela), quando o read model é reconhecidamente efêmero e reprojetável, aceitando o acoplamento de leitura em troca de menos tradução.
- Aprovar uma **consulta direta multi-schema** para uma checagem de unicidade global rara, via a exceção registrada de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md), quando reserva e detecção eventual custam mais do que o problema justifica.

## Veja também

- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): fronteira de agregado = fronteira de módulo; o dado de outro módulo chega resolvido como parâmetro
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): os mecanismos (`Query` do `Contracts`, dispatch, read model, exceção multi-schema) que este documento decide _como_ usar
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): evento como gatilho de compensação (ponto 3) e de limpeza de referência (ponto 4)
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): ACL para sistema externo, o mesmo princípio reaplicado, mais leve, entre módulos internos
- [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md): eliminação orquestrada entre módulos, caso concreto da política de integridade do ponto 4
- [`24-cache.md`](24-cache.md): cópia sincronizada vs. cache — cache expira e recalcula da fonte; read model é atualizado por evento
- [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md): por que a transação de A nunca abarca B, o que força snapshot/reserva/eventual
- [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md): leitura de exibição, onde usar o DTO de `Contracts` alheio diretamente é aceitável
