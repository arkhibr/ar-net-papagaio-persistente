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
}

/**
 * Detalhe mais o ETag do `GET /chamados/{id}`. A versão de concorrência só existe no header,
 * nunca no corpo: é opaca e volta como veio no `If-Match` do `atribuir`.
 */
export interface ChamadoComVersao {
  chamado: ChamadoDetalheDto;
  etag: string | null;
}
