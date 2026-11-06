import { PrioridadeChamado } from './chamado.model';

/** SLA de uma prioridade, em horas (contratos-front.md, seção 6). */
export interface SlaDto {
  prioridade: PrioridadeChamado;
  horas: number;
}

export interface CategoriaDeServicoDto {
  id: string;
  nome: string;
  equipeId: string;
  equipeNome: string | null;
  ativa: boolean;
  slas: SlaDto[];
}

export interface EquipeDto {
  id: string;
  nome: string;
  membros: string[];
}
