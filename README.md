# Arquitetura de referência + pipeline de agentes

Este repositório reúne duas coisas relacionadas, mas distintas:

1. Uma **arquitetura de referência genérica** para sistemas C#/.NET novos que adotem monólito modular + DDD tático + CQRS (`arquitetura/`).
2. Um **pipeline de subagentes do Claude Code** que transforma uma demanda informal de funcionalidade num plano de implementação alinhado a essa arquitetura, camada por camada.

## Estrutura

```text
arquitetura
  01..29-*.md        29 documentos numerados, cada um uma decisão arquitetural genérica
  README.md          índice dos documentos (com seção "Princípios fundamentais")
  apresentacao.md     visão geral ilustrada (diagramas), não substitui os documentos
  agents/            definição "fonte" dos 8 agentes do pipeline + material de apoio
    clarificacao-de-demanda.md, plano-de-arquitetura.md, modelagem-de-dominio.md,
    cqrs-e-casos-de-uso.md, persistencia-e-integracao.md, api-e-composicao.md,
    construcao-de-testes.md, revisao-de-codigo.md
    README.md, coordenador-de-fluxo.md   — material de apoio, NÃO são agentes
```

## Como usar o pipeline

Ponto de entrada: `arquitetura/agents/coordenador-de-fluxo.md`, que descreve a ordem completa das 8 etapas e por que a orquestração acontece na conversa principal, nunca de um subagente chamando outro diretamente.

Resumo da ordem (fluxo padrão, código depois do teste):

1. `clarificacao-de-demanda`: transforma a demanda informal em especificação clarificada, com perguntas em aberto explícitas para quem pediu a funcionalidade
2. `plano-de-arquitetura`: decide, documento por documento, como a especificação se encaixa
3. `modelagem-de-dominio` (Domain) → `cqrs-e-casos-de-uso` (Application) → `persistencia-e-integracao` (Infrastructure) → `api-e-composicao` (Api)
4. `construcao-de-testes`: antes de cada camada (TDD) ou depois de todas (padrão)
5. `revisao-de-codigo`: uma vez, ao final, revisão read-only contra os documentos de referência
