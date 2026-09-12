# Convenções REST na API

## Rota de recurso e de ação de negócio

Recurso é substantivo plural, ação de negócio é sub-rota com verbo:

```
POST   /api/{recursos}                 — criar
GET    /api/{recursos}/{id}            — obter por id
GET    /api/{recursos}                 — listar (paginação em 19-paginacao.md, filtro em 14-filtro-de-dados.md)
POST   /api/{recursos}/{id}/{acao}     — ação de negócio nomeada
```

A rota-base é sempre um substantivo plural na língua de domínio escolhida (ver [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md)), nunca um verbo. Uma ação de negócio que representa uma transição de estado específica do agregado, não a criação/leitura/atualização genérica do recurso, vira uma sub-rota com o verbo daquela ação. O nome do verbo na rota é o mesmo nome do método do agregado que a operação invoca, mantendo a mesma linguagem entre a rota HTTP e o método de domínio.

## Sem `PUT`/`PATCH` genérico

Nunca exponha um `PUT`/`PATCH` genérico que aceite qualquer campo: um `PUT` que aceite um corpo com todos os campos do recurso reintroduziria, na fronteira da API, exatamente o problema que o domínio rico evita por dentro: qualquer cliente poderia setar um campo de estado diretamente, sem passar pela transição controlada do agregado (ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md)). Cada mudança de estado relevante é uma ação nomeada, com rota própria e `Command` próprio, nunca um `update` de propriedade solta.

## Verbo HTTP por tipo de operação

| Operação | Verbo | Idempotente? |
| --- | --- | --- |
| Criar | `POST` | Não, a menos que a rota também exija `Idempotency-Key` |
| Obter/listar | `GET` | Sim, por natureza |
| Ação de negócio que muda estado | `POST` | Depende: exige `Idempotency-Key` quando a ação tem efeito colateral externo ou crítico (ver critério abaixo) |

**Critério objetivo:** a ação exige `Idempotency-Key` sempre que dispara efeito colateral externo ou crítico: chamada a serviço externo com custo/efeito cobrável ou irreversível, ou transição de estado que não pode acontecer duas vezes por reenvio acidental do cliente. Caso contrário, `POST` não idempotente é aceitável.

Se um caso de uso for de fato uma substituição total de recurso ou remoção, ele segue a mesma regra de ação nomeada (`POST /{recursos}/{id}/cancelar`), não um `DELETE` que sugere remover o registro, salvo quando remoção é literalmente a semântica pretendida.

## Obrigatoriedade de `Idempotency-Key`

`Idempotency-Key` é obrigatório em toda ação com efeito colateral externo ou crítico. A chave chega via header HTTP (`Idempotency-Key`), nunca no corpo da requisição; o `Command` a expõe via `IIdempotentCommand` para o `IdempotencyBehavior` (ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)). Regra de quando exigir: qualquer endpoint que dispare uma chamada a um serviço externo com efeito colateral cobrável/irreversível, ou uma transição de estado que não deveria acontecer duas vezes por reenvio acidental do cliente.

## Língua do campo JSON

Nome de campo JSON segue a mesma língua do domínio: campo de negócio no corpo da requisição/resposta usa o nome na língua de domínio escolhida, em `camelCase`. Não traduza para inglês só porque é uma API HTTP: o contrato de dados segue a mesma língua do código por trás dele (ver [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md)). Esta regra não se aplica a endpoints técnicos/genéricos que não representam vocabulário de domínio (autenticação, sessão, saúde do serviço): eles seguem convenção técnica padrão, não substantivo de domínio.

## Controllers vs. Minimal APIs

**Regra direta: Controllers MVC são o ponto de partida default; Minimal APIs são divergência consciente possível, não erro.** A Microsoft recomenda Minimal APIs como default para projetos novos em .NET 8+ (melhor throughput, caminho mais curto para Native AOT), mas a escolha aqui é deliberadamente por Controllers, por dois motivos práticos desta arquitetura: o controller já é fino (só traduz requisição em `Command`/`Query` via `ISender` e o resultado em resposta), então o custo por endpoint é baixo; e atributos declarativos como `[Authorize(Roles = "...")]` (leitura A de [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)) e a organização por `Controller` casam com a promoção de controllers para dentro do módulo descrita em [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md).

Como o controller não carrega regra de negócio, trocar para Minimal APIs é reescrever só a camada de tradução HTTP, sem tocar `Application`/`Domain`. Um sistema que priorize AOT ou densidade de endpoints pode divergir para Minimal APIs registrando o motivo (Nota de aplicação), sem que nada abaixo do `Api` mude.

## Versionamento

Coberto em documento próprio: [`18-versionamento-de-api.md`](18-versionamento-de-api.md).

## Veja também

- [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md): língua e casing
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): quando exigir `Idempotency-Key`, status HTTP por cenário
- [`18-versionamento-de-api.md`](18-versionamento-de-api.md): versionamento por segmento de URL, critério de breaking change, versionamento como fronteira de migração incremental
- [`19-paginacao.md`](19-paginacao.md): página/tamanho, ordenação, limite máximo, ordem de aplicação com filtro de dados
