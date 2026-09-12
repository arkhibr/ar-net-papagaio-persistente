# Versionamento de API

> Preenche a lacuna deixada em aberto em `09-convencoes-rest-api.md` ("Fora de escopo aqui"). Sem fonte de discussão prévia isolada, formaliza um padrão motivado por dois pontos já presentes nesta referência: a promessa de extração futura de módulo, que exige contrato estável ([`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)), e qualquer projeto de modernização que precise migrar funcionalidade de um sistema anterior de forma incremental, sem big bang.

## Onde a versão aparece na rota

O versionamento acontece por segmento de URL:

```
/api/v1/{recursos}
```

A versão é o segundo segmento da rota, depois de `/api/`, implementada com uma biblioteca de versionamento nativa do framework web (ex.: `Asp.Versioning.Http`/`Asp.Versioning.Mvc` no ASP.NET Core).

**Por que segmento de URL, e não header ou media type:** fica visível em log e em métricas sem precisar inspecionar o corpo da requisição, é cacheável por versão sem lógica adicional, um proxy ou o próprio BFF consegue rotear só olhando o path, sem decodificar header customizado (ver [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md)), e é explícito na documentação OpenAPI, que publica uma especificação por versão.

**Geração da spec OpenAPI:** use o gerador nativo `Microsoft.AspNetCore.OpenApi` (.NET 9 removeu o Swashbuckle do template padrão), configurado para emitir um documento por versão de API. A UI de exploração (Scalar, Swagger UI ou outra) é escolha independente, servida sobre esses documentos — a fonte é sempre a spec gerada, uma por versão.

## Quando uma mudança exige nova versão

A API é backward-compatible por padrão; versão nova só em breaking change.

**Não exige nova versão:** adicionar campo opcional novo, adicionar endpoint novo, adicionar valor novo a um enum quando o cliente é tolerante a valor desconhecido.

**Exige nova versão:** remover ou renomear campo, mudar o tipo de um campo existente, mudar a semântica de um campo que já existia, mudar um campo de opcional para obrigatório (quebra qualquer cliente que não o envie) ou de obrigatório para opcional (muda o contrato que o cliente pode assumir como sempre presente), remover endpoint, mudar a estrutura da URL de um recurso já publicado.

## Política de depreciação

Duas versões coexistem por um período mínimo definido antes da remoção da mais antiga.

**Obrigatório, decisão arquitetural:** a resposta da versão depreciada inclui os headers `Deprecation` e `Sunset` (RFC 8594), sinalizando a data-limite. Esse mecanismo técnico é obrigatório desde a primeira depreciação: nunca remover uma versão sem esse aviso.

**Decisão de produto, não arquitetural:** o prazo mínimo de coexistência e a forma de comunicação do desligamento (aviso a cliente, changelog, canal de suporte) ficam fora desta regra — variam por contrato com cada consumidor da API.

## Versionamento como fronteira de migração incremental

Quando um projeto de modernização substitui, módulo por módulo, a implementação de uma funcionalidade que antes vivia num sistema anterior/legado, o versionamento de API não decide **quem** atende a requisição durante a transição (isso é decisão de roteamento na composição raiz/BFF), mas é o mecanismo que sinaliza e protege o cliente quando o contrato muda por causa dessa migração:

- Se a funcionalidade migra e o contrato HTTP permanece compatível, a migração é transparente para o cliente: nenhuma versão nova é necessária, o cutover acontece só na implementação por trás da mesma rota.
- Se o contrato precisa mudar para caber no novo modelo de domínio, isso é o próprio gatilho de nova versão: o cliente recebe aviso e prazo, em vez de a mudança de modelo interno vazar como quebra silenciosa.

Nesses dois casos, o versionamento de API funciona também como ferramenta de migração incremental, além de mecanismo de evolução de contrato em regime estável.

## Fora de escopo aqui

Versionamento de contrato interno entre módulos (`{Modulo}.Contracts`, ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)) e de evento de integração (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)) seguem o mesmo princípio geral (backward-compatible por padrão, breaking change explícito), mas são decisões distintas desta.

## Veja também

- [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md): convenções de rota que este documento complementa
- [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md): promessa de extração futura de módulo, que depende de contrato versionado de forma disciplinada
- [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md): onde o roteamento entre implementação antiga e nova é decidido
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) e [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): contratos internos com o mesmo princípio, versionamento ainda não formalizado
