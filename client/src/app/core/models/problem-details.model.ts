export interface ErrorItem {
  pointer: string | null;
  codigo: string;
  mensagem: string;
}

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: ErrorItem[];
}
