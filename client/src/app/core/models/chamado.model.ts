export enum PrioridadeChamado {
  Baixa = 0,
  Media = 1,
  Alta = 2,
  Critica = 3,
}

export enum StatusChamado {
  Aberto = 0,
  EmAtendimento = 1,
  Resolvido = 2,
  Fechado = 3,
}

export interface ChamadoResumoDto {
  id: string;
  categoriaId: string;
  equipeId: string;
  prioridade: PrioridadeChamado;
  status: StatusChamado;
  abertoEm: string;
  prazoSla: string;
  tecnicoAtribuidoId: string | null;
  escalonado: boolean;
}

export interface ChamadoDetalheDto {
  id: string;
  solicitanteId: string;
  categoriaId: string;
  equipeId: string;
  prioridade: PrioridadeChamado;
  status: StatusChamado;
  abertoEm: string;
  prazoSla: string;
  tecnicoAtribuidoId: string | null;
  notaResolucao: string | null;
  resolvidoEm: string | null;
  fechadoEm: string | null;
  escalonado: boolean;
  dataEscalonamento: string | null;
  rowVersion: string;
}
