import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ChamadoComVersao, ChamadoDetalheDto, ChamadoResumoDto, PrioridadeChamado } from '../models/chamado.model';
import { PagedResponse } from '../models/paged-response.model';

const BASE_URL = '/api/v1/chamados';

/**
 * Os métodos mutáveis recebem a Idempotency-Key da ação (ChaveDeIdempotencia), gerada pela tela
 * e reutilizada nos reenvios (adaptacao-bff-angular.md, F6). URLs sempre relativas: o XSRF nativo
 * do HttpClient só envia o header para URL relativa (F4).
 */
@Injectable({ providedIn: 'root' })
export class ChamadosApiService {
  constructor(private readonly http: HttpClient) {}

  abrir(categoriaId: string, prioridade: PrioridadeChamado, chaveIdempotencia: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(BASE_URL, { categoriaId, prioridade }, { headers: cabecalhos(chaveIdempotencia) });
  }

  obter(id: string): Observable<ChamadoComVersao> {
    return this.http
      .get<ChamadoDetalheDto>(`${BASE_URL}/${id}`, { observe: 'response' })
      .pipe(map((resposta) => ({ chamado: resposta.body!, etag: resposta.headers.get('ETag') })));
  }

  meus(page: number, pageSize: number): Observable<PagedResponse<ChamadoResumoDto>> {
    return this.http.get<PagedResponse<ChamadoResumoDto>>(`${BASE_URL}/meus`, {
      params: { page, pageSize },
    });
  }

  filaDaEquipe(page: number, pageSize: number): Observable<PagedResponse<ChamadoResumoDto>> {
    return this.http.get<PagedResponse<ChamadoResumoDto>>('/api/v1/equipes/fila', {
      params: { page, pageSize },
    });
  }

  /** Autoatribuição: o técnico é o usuário da sessão. O ETag volta como veio do GET. */
  atribuir(id: string, etag: string, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/atribuir`, null, {
      headers: { ...cabecalhos(chaveIdempotencia), 'If-Match': etag },
    });
  }

  devolver(id: string, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/devolver`, {}, { headers: cabecalhos(chaveIdempotencia) });
  }

  reclassificar(id: string, novaPrioridade: PrioridadeChamado, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/reclassificar`, { novaPrioridade }, { headers: cabecalhos(chaveIdempotencia) });
  }

  resolver(id: string, notaResolucao: string, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/resolver`, { notaResolucao }, { headers: cabecalhos(chaveIdempotencia) });
  }

  fechar(id: string, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/fechar`, {}, { headers: cabecalhos(chaveIdempotencia) });
  }

  reabrir(id: string, chaveIdempotencia: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/reabrir`, {}, { headers: cabecalhos(chaveIdempotencia) });
  }
}

function cabecalhos(chaveIdempotencia: string): Record<string, string> {
  return { 'Idempotency-Key': chaveIdempotencia };
}
