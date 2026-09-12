# Paginação de listagens

> Preenche uma referência pendurada em `09-convencoes-rest-api.md` ("listar (paginação/filtro)"), que apontava para um conceito nunca de fato especificado. Sem fonte de discussão prévia isolada.

## Convenção de nomenclatura dos parâmetros

Parâmetros e envelope de resposta usam nomes em inglês, independente da língua de domínio escolhida pelo projeto: paginação é mecânica de transporte, não vocabulário de domínio, mesmo carve-out de endpoints técnicos/genéricos já previsto em [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md).

```http
GET /api/{recursos}?page=1&pageSize=20
```

```json
{
  "items": [
    /* ... */
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 143,
  "totalPages": 8
}
```

Os campos **dentro** de `items` continuam na língua de domínio; só o envelope de paginação em si é convenção técnica.

A mesma regra de `camelCase` para campo JSON, definida em [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md) para o corpo da requisição/resposta, se estende aos parâmetros de query string de paginação (`pageSize`, não `page_size` ou `PageSize`). Isso mantém coerência com o casing do envelope de resposta.

## Escolha entre offset-based e cursor-based

- **`page`/`pageSize` (offset-based)** é o padrão default: simples, suporta "ir para a página N", adequado para grid administrativo com navegação numerada, o caso mais comum na maioria dos sistemas corporativos.
- **Cursor-based** (token opaco apontando para o último item visto) só se justifica quando o **tamanho total do conjunto de dados por trás da consulta** ultrapassa a casa de dezenas de milhares de linhas, ou quando a tela é de rolagem infinita. O critério não é o `pageSize` nem o offset de uma consulta isolada: offset-based degrada conforme o offset cresce independente do tamanho da página, então o que importa é o volume total da tabela/consulta que sustenta a listagem, não o volume de uma página específica. A casa de dezenas de milhares é um critério de exemplo, ajustável pelo sistema concreto conforme o hardware e o padrão de acesso observado.

## Limite de tamanho de página

Todo endpoint de listagem aplica um `pageSize` máximo no servidor, nunca deixado a critério do cliente, independente do que o cliente pedir. Sem limite, um `pageSize` arbitrariamente grande é um vetor barato de negação de serviço. O `pageSize` default também é definido no servidor, nunca deixado ilimitado.

## Ordenação

`?sort=campo` ou `?sort=-campo` (prefixo `-` para descendente). O nome do campo de ordenação segue a língua de domínio, pois é campo de negócio. A lista de campos ordenáveis por endpoint é uma allowlist explícita, decidida pelo próprio handler/endpoint, nunca qualquer propriedade do agregado aceita livremente: isso vazaria detalhe de schema/implementação para o contrato público. O critério recomendado para o que entra na allowlist é permitir só colunas que já são index no banco. Ordenar por coluna sem index introduz full scan a cada chamada, um custo que cresce junto com a tabela.

## Ordem de aplicação das etapas

A ordem é sempre filtro de linha → ordenação → paginação, sempre no banco: filtro de linha → `OrderBy` → `Skip`/`Take`, traduzida para a query nativa do banco até o fim da composição. Paginação aplica sobre o resultado já filtrado por linha (ver [`14-filtro-de-dados.md`](14-filtro-de-dados.md)). `totalItems` reflete o que o usuário pode ver, nunca o total irrestrito da tabela. Nunca materializar a lista inteira em memória para depois paginar na aplicação.

## Paginação e composição na borda

Se um endpoint de "composição na borda" (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)) precisar paginar um resultado agregado de mais de um módulo, isso é sinal de que a composição já ultrapassou o escopo de "poucos módulos, baixo volume": paginar depois de já ter buscado tudo de N módulos perde a vantagem do `Skip`/`Take` no banco, e indica que a leitura deveria virar read model dedicado.

## Veja também

- [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md): referência original ao conceito de paginação, agora coberta por este documento
- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): filtro por linha, aplicado antes da paginação
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): por que composição na borda paginada é sinal de promoção para read model
- [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md): carve-out de convenção técnica em inglês, mesmo usado aqui
