# Validação sintática (FluentValidation) vs. invariante de domínio (`DomainException`)

## O teste de decisão

> **Essa checagem depende só do valor do próprio campo, isolado, sem consultar nenhum outro dado de contexto ou estado de negócio?**

- **Sim** → validação sintática. Mora no `AbstractValidator<TCommand>` da `Application`, executada pelo `ValidationBehavior` antes do handler.
- **Não, depende do estado atual de um agregado, de outra entidade, ou de uma regra que pode mudar conforme o negócio evolui** → invariante de domínio. Mora no método do agregado, lançando `DomainException`.

## Terceira categoria: autorização

Uma checagem que depende de **quem está pedindo** (não só do conteúdo do `Command`, nem do estado do agregado) não é nem sintática, nem invariante — é autorização. Cai no mecanismo de [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md). O erro mais comum é tratar uma checagem desse tipo como se fosse validação de entrada porque ela "parece" checar um campo do `Command`, quando na prática ela também depende de `ICurrentUser`, o que a tira do escopo de um `AbstractValidator`.

Esse teste ("depende de quem está pedindo?") é equivalente ao usado em [`02-dominio-hibrido.md`](02-dominio-hibrido.md) para a mesma fronteira — "a regra continuaria valendo se disparada por um processo automatizado, sem usuário logado?". Os dois sempre chegam à mesma conclusão; use qualquer um dos dois, o que for mais natural no contexto.

## Caso-borda: o próprio agregado sendo criado ou modificado

Às vezes a checagem "quase sintática" parece depender de um agregado, mas esse agregado é justamente o que está sendo criado ou modificado pela operação atual. Nesse caso o teste de decisão continua o mesmo, só exige atenção a **contra o que** a checagem compara. Se ela só usa o próprio valor sendo validado no momento da criação (formato, faixa, combinação de campos do mesmo `Command`), ainda é sintática, mesmo que "pareça" checar o agregado. Se ela precisa comparar contra estado já persistido — do próprio agregado em uma versão anterior, ou de qualquer outra entidade no banco —, é invariante, porque depende de algo que só o agregado (ou um repositório, a partir do handler) consegue responder.

## Consulta a repositório dentro do validador

O validador nunca consulta o repositório para decidir regra de negócio. Mesmo quando a biblioteca de validação suporta regra assíncrona contra banco, evite usar isso para decidir algo que é regra de negócio. Resolva a checagem no handler, consultando o repositório explicitamente e decidindo o `Result.Failure` ali: fica visível no fluxo do caso de uso, testável com um fake de repositório, e não esconde uma consulta a banco dentro de uma classe cujo nome (`Validator`) sugere só checagem de forma.

## Quando a mesma regra existe nos dois lugares

A duplicação é intencional quando um value object precisa nascer válido independente de quem o constrói (um teste de unidade do `Domain`, um script de importação, o próprio validador), e o validador do `Command` valida o mesmo campo porque `FluentValidation` agrega todas as falhas de uma vez (útil para devolver uma lista completa de erros ao cliente), enquanto o agregado lançaria só na primeira violação encontrada. É a mesma fronteira que decide o formato da resposta de erro: lista de erros por padrão para o lado sintático, item único por padrão para o lado do agregado — ver a seção "Lista de erros de negócio" em [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md).

## O que dá errado ao inverter a escolha

- **Regra de negócio dentro do validador:** fica invisível para qualquer código que construa o agregado sem passar pelo `Command`/pipeline: um job em lote, uma migração de dados, um teste que instancia o agregado direto.
- **Checagem de forma dentro do agregado:** rejeita na primeira violação em vez de agregar todos os problemas de uma vez, pior experiência de erro para quem chama a API, e mistura "o dado está bem formado" com "a operação de negócio é válida" no mesmo método.

## Veja também

- [`02-dominio-hibrido.md`](02-dominio-hibrido.md): onde a regra de negócio mora, e por quê
- [`03-commands-e-queries.md`](03-commands-e-queries.md): quando um `Command` precisa de validador
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): como `ValidationException` e `DomainException` se traduzem em status HTTP
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): a terceira categoria, dependente de quem está pedindo
