# Agentes de apoio ao ciclo de implementação

Esta pasta reúne um conjunto de definições de subagente (formato reconhecido pelo Claude Code em `.claude/agents/*.md`) que cobre o ciclo completo de uma funcionalidade nova construída sobre esta arquitetura de referência: clarear a demanda, estabelecer um plano de arquitetura, implementar cada camada, construir os testes e revisar o código.

## Relação com os documentos numerados desta pasta

Os documentos numerados (`01` a `29`) são a fonte de verdade das regras — mantidos e evoluídos neste repositório central. Os agentes abaixo **incorporam essas regras diretamente no próprio texto**, em vez de apontar para o documento em tempo de execução: cada agente é escrito para ser autossuficiente, sem depender de ler `arquitetura-de-referencia/` (ou qualquer pasta equivalente) para operar. Isso implica duplicação deliberada — a mesma regra (ex.: ordem de pipeline behaviors, autorização por recurso) aparece, reformulada conforme a camada, em mais de um agente. Quando um documento numerado muda, os agentes que incorporam aquela regra precisam ser atualizados junto; não há um mecanismo automático de sincronização.

## Este é material de referência, não um pacote instalado

Estes arquivos não ficam ativos automaticamente neste repositório — o Claude Code descobre subagentes em `.claude/agents/`, não dentro de `arquitetura-de-referencia/`. Um sistema concreto que adote esta arquitetura de referência copia os arquivos de agente desta pasta para o próprio `.claude/agents/`, ajustando o que for específico daquele sistema (nome do domínio, eventuais notas de aplicação próprias desse sistema concreto). Como os agentes são autossuficientes, essa cópia funciona mesmo que o sistema concreto não tenha (ou não replique) a pasta `arquitetura-de-referencia/` completa — só os documentos numerados, se mantidos separadamente, ficam disponíveis num repositório central para consulta humana, não para os agentes lerem em tempo de execução. Dois arquivos desta pasta não são agentes e não devem ser copiados como se fossem: este `README.md` e [`coordenador-de-fluxo.md`](coordenador-de-fluxo.md).

## Pipeline

| Ordem | Agente | Papel |
| --- | --- | --- |
| 1 | [`clarificacao-de-demanda`](clarificacao-de-demanda.md) | Transforma uma solicitação informal em especificação clarificada, com perguntas em aberto explícitas |
| 2 | [`plano-de-arquitetura`](plano-de-arquitetura.md) | Decide, documento por documento, como a especificação se encaixa nesta arquitetura de referência |
| 3 | [`modelagem-de-dominio`](modelagem-de-dominio.md) | Implementa a camada Domain |
| 4 | [`cqrs-e-casos-de-uso`](cqrs-e-casos-de-uso.md) | Implementa a camada Application |
| 5 | [`persistencia-e-integracao`](persistencia-e-integracao.md) | Implementa a camada Infrastructure |
| 6 | [`api-e-composicao`](api-e-composicao.md) | Implementa a camada Api |
| 7 | [`construcao-de-testes`](construcao-de-testes.md) | Escreve os testes — antes (TDD) ou depois (padrão) das camadas 3–6, ver `coordenador-de-fluxo.md` |
| 8 | [`revisao-de-codigo`](revisao-de-codigo.md) | Revisão read-only contra os documentos de arquitetura, ao final |

Ver [`coordenador-de-fluxo.md`](coordenador-de-fluxo.md) para como essas oito etapas se conectam, inclusive a escolha entre fluxo TDD e fluxo padrão (código → teste), e para a ressalva sobre por que esse arquivo é um roteiro, não um agente invocável.

## Veja também

- [`../README.md`](../README.md): índice dos documentos desta arquitetura de referência, fonte das regras que os agentes abaixo incorporam (consulta humana; os agentes não dependem dele em tempo de execução)
- [`coordenador-de-fluxo.md`](coordenador-de-fluxo.md): como orquestrar as oito etapas
