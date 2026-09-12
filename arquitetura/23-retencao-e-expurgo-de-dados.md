# Retenção e expurgo de dados

> Racional completo de proteção de dados/LGPD-GDPR não é objeto deste documento: isso é decisão jurídica/compliance, não arquitetural. Este documento cobre o mecanismo. Complementa [`14-filtro-de-dados.md`](14-filtro-de-dados.md) (quem vê o dado enquanto ele existe) e [`15-observabilidade.md`](15-observabilidade.md) (o que nunca vai para o log). Reaproveita [`21-auditoria.md`](21-auditoria.md) para registrar quando um expurgo acontece, e [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) para varrer múltiplos módulos num pedido de acesso/eliminação.

## Política de retenção por entidade

Toda entidade com dado pessoal declara uma política de retenção explícita: nenhum dado pessoal fica no banco "para sempre por padrão" nem "some quando alguém lembrar de apagar". Cada tipo de dado tem uma política decidida conscientemente:

| Categoria | Política típica |
| --- | --- |
| Sessão/estado técnico transitório | Expurgo físico após expiração, curto prazo |
| Log técnico estruturado | Prazo curto, expurgo automático |
| Registro de auditoria | Prazo longo, decidido por compliance/jurídico, não por arquitetura |
| Registro com obrigação legal de retenção (contábil, regulatório, educacional, conforme o domínio) | **Exceção documentada**: retenção além do que o titular do dado poderia pedir para eliminar |
| Dado pessoal sem obrigação legal de retenção | Expurgo ou anonimização em prazo curto após o propósito original terminar |

A linha de "obrigação legal de retenção" é a mais sensível: a arquitetura não decide sozinha reter um dado para sempre. Isso é decisão jurídica sobre uma obrigação real, registrada explicitamente como exceção consciente, nunca como "esquecemos de implementar expurgo".

## Conflito entre obrigação legal e pedido de eliminação

Obrigação legal de retenção resolve-se por anonimização, não por ignorar o pedido: quando existe obrigação legal de manter um registro e, ao mesmo tempo, um titular pede eliminação dos próprios dados pessoais, a resposta é **anonimização seletiva**: os campos que identificam a pessoa são mascarados/removidos, mantendo o registro que a obrigação legal exige, sem mais vincular esse registro a uma pessoa identificável.

## Direito de acesso e eliminação

Um pedido de acesso ou eliminação de dado pessoal tipicamente atravessa vários módulos: um único pedido do titular, múltiplos módulos envolvidos. É o mesmo problema de leitura/escrita cross-módulo já resolvido em [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md):

- **Acesso**: composição na borda, despachando uma `Query` de exportação para cada módulo dono de dado pessoal do titular, e compondo um relatório único. O fluxo de acesso **sempre retorna todo o dado pessoal do titular**, sem filtrar por obrigação legal de retenção: obrigação de retenção restringe o direito de eliminar, não o direito de acessar.
- **Eliminação/anonimização**: orquestração de escrita entre módulos, análoga em forma ao acesso (mesmo padrão de despachar para múltiplos módulos), mas diferente em decisão. Um `Command` de nível superior despacha, para cada módulo com dado do titular, o `Command` de eliminação/anonimização daquele módulo, através do `Contracts` de cada um. Antes de decidir entre eliminar de fato ou anonimizar mantendo só o necessário, cada módulo consulta, por tipo de dado que possui, se há obrigação legal de retenção ativa para aquele dado (ver tabela de política de retenção acima): havendo obrigação, o módulo anonimiza seletivamente; não havendo, elimina de fato. Acesso e eliminação não reaproveitam o mesmo `Command`/`Query`: são fluxos distintos, cada um com sua própria decisão por módulo. O orquestrador de nível superior não conhece o schema interno nem a política de retenção de nenhum módulo, só dispara o `Command` correspondente em cada um.

## Todo expurgo/anonimização é auditado

Eliminar ou anonimizar dado pessoal é, em si, uma operação crítica: usa o mecanismo de [`21-auditoria.md`](21-auditoria.md) (`IAuditable`), registrando quem solicitou, quando e o motivo.

## Conflito entre reter o registro de auditoria e o pedido de eliminação do próprio ator

Um registro de auditoria pode documentar uma operação feita por um usuário que, mais tarde, pede eliminação dos próprios dados pessoais, e a obrigação legal de reter aquele registro de auditoria pode ter prazo mais longo do que o titular pediu para ser esquecido. Esse é o mesmo conflito já descrito acima ("obrigação legal de retenção resolve-se por anonimização, não por ignorar o pedido"), aplicado especificamente ao registro de auditoria: o identificador do ator (`ActorUserId` ou campo equivalente) é anonimizado seletivamente, mas a estrutura do evento (ação, timestamp, recurso afetado) é mantida, preservando o valor de compliance do registro sem manter o vínculo com uma pessoa identificável.

**Exemplo, ajustável por sistema concreto:** o critério exato de quando essa anonimização se aplica — por exemplo, "anonimizar o `ActorUserId` do registro de auditoria N dias após o pedido de eliminação, desde que o prazo mínimo de retenção legal já tenha decorrido" — não é decisão desta arquitetura de referência. É parâmetro de negócio/compliance de cada sistema, como qualquer outro prazo desta tabela de política de retenção.

## O que este documento não decide

Prazo exato de cada política é decisão jurídica/compliance, não arquitetural: este documento define o mecanismo (declarar política, expurgo via job agendado, anonimização como resposta a obrigação legal concorrente), não os números. O "prazo curto" citado na tabela acima é um placeholder: quem decide o valor concreto (dias, meses) é o negócio/compliance de cada sistema, nunca esta arquitetura de referência. Uma vez decidido, porém, o prazo é uma decisão arquitetural de implementação: deve estar declarado em configuração ou código (ex.: `IOptions<T>` tipado, atributo no agregado, constante versionada), nunca apenas documentado em prosa ou numa wiki separada do código. Do contrário, o prazo real diverge do prazo aplicado sem ninguém perceber.

## Veja também

- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): quem vê o dado enquanto ele existe
- [`15-observabilidade.md`](15-observabilidade.md): o que nunca vai para o log
- [`21-auditoria.md`](21-auditoria.md): registro de quando um expurgo/anonimização acontece
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): composição na borda (para acesso) e orquestração de escrita (para eliminação) entre módulos
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): onde o job periódico de expurgo roda
