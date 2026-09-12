# Integração com sistemas externos e legados

Toda integração com um sistema externo ou legado passa por um Anti-Corruption Layer: uma camada de adapter que traduz o vocabulário e o protocolo do sistema de origem para o vocabulário do próprio domínio, sem deixar esse vocabulário externo vazar para dentro.

> Padrão de origem: Anti-Corruption Layer (Eric Evans, _Domain-Driven Design_).

## Onde o adapter vive

Cada integração externa/legada tem um contrato na `Application` do módulo que a usa, e uma implementação concreta num projeto de infraestrutura compartilhado (equivalente ao `Shared.Infrastructure.*`):

```text
{Modulo}.Application
  IServicoExterno            — interface, só o que o módulo precisa saber

Infrastructure.Legacy (ou equivalente)
  ServicoExternoAdapter       — implementação concreta
  ServicoExternoHealthCheck
  ServicoExternoResilienceOptions
```

O módulo de domínio/aplicação nunca conhece o protocolo, o formato ou o mecanismo de autenticação do sistema de origem: o adapter traduz isso para o vocabulário do próprio domínio. O projeto de infraestrutura do adapter é referenciado só pela composição raiz (`Api`/`Worker`), nunca por `Domain` ou `Application` de nenhum módulo.

## Resiliência do adapter

Todo adapter de legado é resiliente por padrão, não por exceção: num monólito, todos os módulos compartilham o mesmo pool de threads do processo. Uma chamada síncrona a um sistema externo sem timeout/circuit breaker não afeta só o módulo que a fez: uma chamada lenta pode consumir threads suficientes para degradar módulos completamente não relacionados, mesmo que só uma integração específica esteja com problema. Isso é um risco específico do monólito (em microserviços, o blast radius de uma integração lenta fica contido ao próprio serviço).

Todo adapter usa uma biblioteca de resiliência HTTP (ex.: `Microsoft.Extensions.Http.Resilience`), com no mínimo:

- **Timeout** por chamada, curto o suficiente para não travar a requisição do usuário esperando o sistema externo responder.
- **Circuit breaker**, para parar de tentar um sistema que já está caído, em vez de acumular requisições penduradas.
- **Retry** só para o que é seguro re-tentar (operação idempotente do lado de lá); nunca retry automático em cima de uma chamada que já pode ter causado efeito colateral.

Cada adapter registra sua própria checagem de health check, agregada num único endpoint (ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)).

## Identidade técnica, não identidade do usuário

Quando o adapter usa uma credencial técnica (BasicAuth, API key, client credentials), essa credencial autentica a aplicação, nunca o usuário final. Quando o sistema de destino precisa saber quem originou a operação, o identificador do ator é gerado pelo próprio backend (`ICurrentUser.UserId`, ver [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md)), nunca reaproveitado de header ou de credencial do usuário.

## Padrão de chamada síncrona vs. assíncrona

| Cenário | Quem chama o adapter |
| --- | --- |
| Usuário precisa do dado/resultado na mesma resposta HTTP | Camada Api, síncrono, com timeout curto e fallback explícito se o sistema externo estiver fora do ar |
| Efeito colateral pode acontecer depois, fora da resposta HTTP | Worker, via Outbox (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)) |

Um adapter chamado de forma síncrona sem um fallback definido para "sistema externo indisponível" não deveria ser mesclado ao código: decidir explicitamente, por adapter, o que a operação faz quando a chamada falha é parte do desenho do adapter, não um detalhe a decidir depois.

O trade-off real entre as duas colunas da tabela acima é qual risco é aceitável para aquele fluxo. Chamada síncrona aceita falha parcial visível na hora (por isso exige fallback explícito), mas mantém usuário e sistema externo consistentes no mesmo instante: se deu certo, o usuário sabe. Chamada assíncrona via Outbox garante reentrega e sobrevive a queda do processo, mas a operação do usuário já foi confirmada antes de o efeito colateral no sistema externo de fato acontecer: o usuário pode ver "sucesso" antes de o sistema externo ter processado, ou mesmo antes de descobrir que aquele sistema está fora do ar. Escolher entre as duas colunas é escolher qual desses dois riscos o fluxo específico pode assumir, não qual mecanismo é tecnicamente superior.

## Veja também

- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): quando o adapter é chamado pelo worker em vez de sincronamente
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): health check agregado
- [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md): origem de `ICurrentUser.UserId` propagado para o sistema externo
