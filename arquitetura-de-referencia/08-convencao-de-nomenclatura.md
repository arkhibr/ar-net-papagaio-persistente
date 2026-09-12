# Convenção de nomenclatura

> Esta convenção já registra explicitamente ser escolha de estilo de projeto, não regra universal de DDD.

## Língua do vocabulário

A regra geral é vocabulário de negócio na língua do domínio, termo técnico em inglês:

- **Linguagem ubíqua do domínio → a língua que o especialista de negócio usa.** Nome de agregado, entidade, método de ação de negócio, propriedade que representa um conceito de negócio, evento de domínio, exceção de domínio, nome de módulo: seguem a língua em que o time de negócio fala sobre o próprio domínio. Este documento não fixa qual língua é essa: é decisão de cada sistema, registrada explicitamente (ver "Nota de aplicação", abaixo).
- **Termo técnico/de infraestrutura → inglês, sempre**, independente da língua de domínio escolhida: `Command`, `Query`, `Handler`, `Repository`, `Specification`, `Validator`, `Event`, `Exception`, `DbContext`, `UnitOfWork`, `Module`, `Contracts`.
- **Regra de bolso:** nome que o especialista de negócio reconheceria ou usaria numa conversa sobre o domínio → língua do negócio. Nome que só existe no vocabulário de padrão/framework/infraestrutura → inglês.
- **Exceção: endpoint/conceito técnico e genérico não segue a língua de domínio.** Autenticação, sessão, saúde do serviço (`health check`) e paginação como mecânica de transporte não representam vocabulário de domínio, mesmo aparecendo na API pública. Seguem convenção técnica padrão em inglês, não a língua de domínio escolhida, mesmo quando o restante do sistema é nomeado na língua do negócio.

## O prefixo do sistema/bounded context segue a mesma regra do domínio

O nome que identifica o sistema ou bounded context como um todo (o segmento inicial de `{Sistema}.Domain`, `{Sistema}.Modules.{Modulo}`) é vocabulário de domínio, não termo técnico: mesma categoria de `Matricula`/`Boletim` num agregado. Segue a língua do negócio, não é escolhido por ser mais curto em inglês.

## Sufixo padrão por tipo

| Tipo | Convenção | Exemplo |
| --- | --- | --- |
| Command | `{CasoDeUso}Command` | — |
| Query | `{CasoDeUso}Query` | — |
| Handler | `{NomeDaMensagem}Handler` | — |
| Validador | `{NomeDaMensagem}Validator` | — |
| Evento de domínio | `{Entidade}{ParticípioPassado}Event` | — |
| Evento de integração | `{Entidade}{ParticípioPassado}IntegrationEvent` | marca a diferença entre evento in-process (dentro do módulo) e evento que atravessa módulo/processo (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)) |
| Adapter de integração externa | `{Sistema}Adapter` | ver [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) |
| Exceção | `{Motivo}Exception` | — |

## O que evitar

- Traduzir literalmente um termo técnico do framework para a língua de domínio. O nome técnico é o nome oficial do padrão; traduzir cria dois vocabulários para o mesmo conceito.
- Misturar língua dentro do mesmo tipo de artefato (um `Command` na língua de domínio, o próximo em inglês). A inconsistência tende a se justificar caso a caso e se acumular sem ninguém decidir isso conscientemente.
- Usar o nome de domínio em inglês só porque é mais curto, quando o especialista de negócio usa outra língua. O código segue o termo que o negócio usa, não o termo mais econômico de digitar.

## Nota de aplicação

Migração de vocabulário existente segue a mesma regra: um sistema com módulos e rotas nomeados em inglês, mas cujo vocabulário de negócio real é em outra língua, corrige isso traduzindo também o prefixo do sistema/bounded context, não só os artefatos internos, já que o prefixo é vocabulário de domínio, não termo técnico.

## Veja também

- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): distinção entre evento de domínio e evento de integração
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): convenção de nome de adapter
- [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md): mesma regra de língua aplicada a rotas e campos JSON
