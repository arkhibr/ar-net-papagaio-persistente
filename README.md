# Arquitetura de referência + pipeline de agentes

Este repositório reúne duas coisas relacionadas, mas distintas:

1. Uma **arquitetura de referência genérica** para sistemas C#/.NET novos que adotem monólito modular + DDD tático + CQRS (`arquitetura-de-referencia/`), não amarrada a nenhum domínio de negócio específico.
2. Um **pipeline de subagentes do Claude Code** que transforma uma demanda informal de funcionalidade num plano de implementação alinhado a essa arquitetura, camada por camada.

## Estrutura

```text
arquitetura-de-referencia/
  01..29-*.md        29 documentos numerados, cada um uma decisão arquitetural genérica
  00-revisao-e-lacunas.md   revisão da referência (decisões, defasagens, lacunas) — não é regra, é índice de trabalho
  README.md          índice dos documentos (com seção "Princípios fundamentais")
  apresentacao.md     visão geral ilustrada (diagramas), não substitui os documentos
  agents/            definição "fonte" dos 8 agentes do pipeline + material de apoio
    clarificacao-de-demanda.md, plano-de-arquitetura.md, modelagem-de-dominio.md,
    cqrs-e-casos-de-uso.md, persistencia-e-integracao.md, api-e-composicao.md,
    construcao-de-testes.md, revisao-de-codigo.md
    README.md, coordenador-de-fluxo.md   — material de apoio, NÃO são agentes

.claude/agents/       cópia ativa dos arquivos acima — é daqui que o Claude Code carrega os
                      subagentes de fato (ver "Manutenção", abaixo)

plano.md                  exemplo de saída do pipeline: Sistema de Controle de Matrículas e Notas
arquitetura-sistema.md    topologia completa do mesmo exemplo (cliente Angular, BFF, banco)
plano2.md                 segundo exemplo: criação e autenticação de usuários (`User`)

perguntas.md, pendencias.md, candidatos.md   notas de trabalho soltas, não fazem parte do
                                              pipeline nem da arquitetura de referência
```

## Como usar o pipeline

Ponto de entrada: `arquitetura-de-referencia/agents/coordenador-de-fluxo.md`, que descreve a ordem completa das 8 etapas e por que a orquestração acontece na conversa principal, nunca de um subagente chamando outro diretamente.

Resumo da ordem (fluxo padrão, código depois do teste):

1. `clarificacao-de-demanda`: transforma a demanda informal em especificação clarificada, com perguntas em aberto explícitas para quem pediu a funcionalidade
2. `plano-de-arquitetura`: decide, documento por documento, como a especificação se encaixa
3. `modelagem-de-dominio` (Domain) → `cqrs-e-casos-de-uso` (Application) → `persistencia-e-integracao` (Infrastructure) → `api-e-composicao` (Api)
4. `construcao-de-testes`: antes de cada camada (TDD) ou depois de todas (padrão)
5. `revisao-de-codigo`: uma vez, ao final, revisão read-only contra os documentos de referência

`plano.md` e `plano2.md` são exemplos reais de saída das etapas 1 e 2, já rodadas até aqui; nenhuma camada de código foi escrita ainda neste repositório.

## Manutenção

Os 8 agentes citados acima existem em dois lugares por design: `arquitetura-de-referencia/agents/` (fonte, genérica, pensada para ser copiada por qualquer sistema que adote esta arquitetura) e `.claude/agents/` (cópia ativa, o que o Claude Code efetivamente carrega neste workspace). Sempre que um arquivo em `arquitetura-de-referencia/agents/` mudar, copie-o de novo para `.claude/agents/`: não há sincronização automática entre as duas pastas.
