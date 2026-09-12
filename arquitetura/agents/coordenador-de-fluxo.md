# Como orquestrar o pipeline

Este documento não é um agente — não tem frontmatter de subagente e não deve ser copiado para `.claude/agents/`. Um subagente do Claude Code não invoca outro subagente de forma confiável, nem interrompe a própria execução para perguntar algo ao usuário no meio do caminho — só a conversa principal tem as duas capacidades. Por isso a orquestração fica a cargo de quem está conduzindo essa conversa (a sessão principal), lendo este roteiro e invocando cada agente, na ordem certa, um de cada vez.

Diferente deste roteiro, os oito agentes citados abaixo não dependem de ler nenhum documento (nem este, nem os numerados de `arquitetura-de-referencia/`) para operar: cada um incorpora as regras que aplica diretamente no próprio texto. Este arquivo é só o mapa de orquestração para quem conduz a sessão principal, não um insumo que os agentes consultam.

## Passo 0: perguntar o fluxo

Antes de invocar qualquer agente de implementação, pergunte ao solicitante: **fluxo TDD ou fluxo padrão (código → teste)?** A resposta decide a ordem dos passos 3 a 7, abaixo. Não muda os passos 1, 2 e 8, que são sempre os mesmos independente da escolha.

## Fluxo padrão: código → teste

```text
1. clarificacao-de-demanda
2. plano-de-arquitetura
3. modelagem-de-dominio          (Domain)
4. cqrs-e-casos-de-uso           (Application)
5. persistencia-e-integracao     (Infrastructure)
6. api-e-composicao              (Api)
7. construcao-de-testes          (todas as camadas já implementadas)
8. revisao-de-codigo
```

## Fluxo TDD: teste antes de cada camada

```text
1. clarificacao-de-demanda
2. plano-de-arquitetura
3. construcao-de-testes (Domain)        → modelagem-de-dominio        → confirma teste passa
4. construcao-de-testes (Application)   → cqrs-e-casos-de-uso         → confirma teste passa
5. construcao-de-testes (Infrastructure)→ persistencia-e-integracao   → confirma teste passa
6. construcao-de-testes (Api)           → api-e-composicao            → confirma teste passa
7. revisao-de-codigo
```

No fluxo TDD, `construcao-de-testes` é invocado uma vez por camada, sempre imediatamente antes do agente de implementação daquela camada — nunca todas as camadas de uma vez no início. O teste precisa falhar antes da implementação existir, e falhar pelo motivo certo (comportamento ainda não implementado, não erro de compilação ou de configuração do próprio teste). A confirmação de que o teste passou depois (verde) é responsabilidade do agente de implementação daquela camada, que tem acesso a `Bash` para rodar a suíte.

Se `construcao-de-testes` não conseguir descrever um teste que falhe de forma significativa para uma camada, é sinal de que o plano do passo 2 está incompleto para aquela camada — pare e revise o plano, não force um teste vazio só para preencher a etapa.

## Passo 8 sempre ao final, independente do fluxo escolhido

`revisao-de-codigo` roda uma vez, depois de todas as camadas implementadas e testadas — nunca por camada isolada, porque parte do que ele verifica só é visível olhando mais de uma camada ao mesmo tempo (ex.: se a ordem de pipeline behavior declarada em `Application` bate com o que `Api` realmente registra na composição raiz).

## O que fazer com perguntas em aberto e lacunas sinalizadas

`clarificacao-de-demanda` pode devolver perguntas que só o solicitante resolve; `plano-de-arquitetura` pode sinalizar decisões sem documento de arquitetura por trás. Nos dois casos, quem conduz a orquestração leva isso ao solicitante antes de prosseguir — nenhum agente downstream deveria receber uma pergunta em aberto ainda não resolvida como se fosse uma decisão fechada.

## Veja também

- [`README.md`](README.md): índice do pipeline e onde este roteiro se encaixa
- [`clarificacao-de-demanda.md`](clarificacao-de-demanda.md), [`plano-de-arquitetura.md`](plano-de-arquitetura.md): passos 1 e 2
- [`modelagem-de-dominio.md`](modelagem-de-dominio.md), [`cqrs-e-casos-de-uso.md`](cqrs-e-casos-de-uso.md), [`persistencia-e-integracao.md`](persistencia-e-integracao.md), [`api-e-composicao.md`](api-e-composicao.md): passos 3–6
- [`construcao-de-testes.md`](construcao-de-testes.md): passo 7 (padrão) ou intercalado (TDD)
- [`revisao-de-codigo.md`](revisao-de-codigo.md): passo 8
