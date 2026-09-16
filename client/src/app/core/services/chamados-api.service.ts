import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ChamadoDetalheDto, ChamadoResumoDto, PrioridadeChamado } from '../models/chamado.model';
import { PagedResponse } from '../models/paged-response.model';

const BASE_URL = '/api/v1/chamados';

@Injectable({ providedIn: 'root' })
export class ChamadosApiService {
  constructor(private readonly http: HttpClient) {}

  abrir(categoriaId: string, prioridade: PrioridadeChamado): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(BASE_URL, { categoriaId, prioridade });
  }

  obter(id: string): Observable<ChamadoDetalheDto> {
    return this.http.get<ChamadoDetalheDto>(`${BASE_URL}/${id}`);
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

  atribuir(id: string, tecnicoId: string, rowVersion: string): Observable<void> {
    return this.http.post<void>(
      `${BASE_URL}/${id}/atribuir`,
      { tecnicoId },
      { headers: { 'If-Match': `"${rowVersion}"` } },
    );
  }

  devolver(id: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/devolver`, {});
  }

  reclassificar(id: string, novaPrioridade: PrioridadeChamado): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/reclassificar`, { novaPrioridade });
  }

  resolver(id: string, notaResolucao: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/resolver`, { notaResolucao });
  }

  fechar(id: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/fechar`, {});
  }

  reabrir(id: string): Observable<void> {
    return this.http.post<void>(`${BASE_URL}/${id}/reabrir`, {});
  }
}
