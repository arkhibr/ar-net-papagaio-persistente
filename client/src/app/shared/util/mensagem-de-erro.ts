import { ProblemDetails } from '../../core/models/problem-details.model';
import { PrioridadeChamado } from '../../core/models/chamado.model';

/** Texto para mostrar num formulário a partir do ProblemDetails (errors[] primeiro, arquitetura/06). */
export function mensagemDeErro(problema: ProblemDetails, padrao: string): string {
  const mensagens = problema.errors?.map((e) => e.mensagem).filter(Boolean) ?? [];
  return mensagens.length > 0 ? mensagens.join(' ') : (problema.detail ?? padrao);
}

export const PRIORIDADES: { valor: PrioridadeChamado; rotulo: string }[] = [
  { valor: PrioridadeChamado.Baixa, rotulo: 'Baixa' },
  { valor: PrioridadeChamado.Media, rotulo: 'Média' },
  { valor: PrioridadeChamado.Alta, rotulo: 'Alta' },
  { valor: PrioridadeChamado.Critica, rotulo: 'Crítica' },
];
