import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CategoriaDeServicoDto, EquipeDto, SlaDto } from '../models/catalogo.model';

const CATEGORIAS = '/api/v1/categorias-de-servico';
const EQUIPES = '/api/v1/equipes';

/**
 * Catálogo (contratos-front.md, seções 7.11 e 7.12). As rotas de administração exigem
 * Supervisor e não usam Idempotency-Key (arquitetura/09).
 */
@Injectable({ providedIn: 'root' })
export class CatalogoApiService {
  constructor(private readonly http: HttpClient) {}

  /** Só as ativas por padrão; incluirInativas é só para Supervisor. */
  listarCategorias(incluirInativas = false): Observable<CategoriaDeServicoDto[]> {
    return this.http.get<CategoriaDeServicoDto[]>(CATEGORIAS, { params: { incluirInativas } });
  }

  criarCategoria(nome: string, equipeId: string, slas: SlaDto[]): Observable<string> {
    return this.http.post<{ id: string }>(CATEGORIAS, { nome, equipeId, slas }).pipe(map((r) => r.id));
  }

  renomearCategoria(id: string, nome: string): Observable<void> {
    return this.http.post<void>(`${CATEGORIAS}/${id}/renomear`, { nome });
  }

  transferirCategoria(id: string, equipeId: string): Observable<void> {
    return this.http.post<void>(`${CATEGORIAS}/${id}/transferir`, { equipeId });
  }

  definirSlas(id: string, slas: SlaDto[]): Observable<void> {
    return this.http.post<void>(`${CATEGORIAS}/${id}/definir-slas`, { slas });
  }

  inativarCategoria(id: string): Observable<void> {
    return this.http.post<void>(`${CATEGORIAS}/${id}/inativar`, null);
  }

  reativarCategoria(id: string): Observable<void> {
    return this.http.post<void>(`${CATEGORIAS}/${id}/reativar`, null);
  }

  listarEquipes(): Observable<EquipeDto[]> {
    return this.http.get<EquipeDto[]>(EQUIPES);
  }

  criarEquipe(nome: string): Observable<string> {
    return this.http.post<{ id: string }>(EQUIPES, { nome }).pipe(map((r) => r.id));
  }

  renomearEquipe(id: string, nome: string): Observable<void> {
    return this.http.post<void>(`${EQUIPES}/${id}/renomear`, { nome });
  }

  vincularMembro(equipeId: string, usuarioId: string): Observable<void> {
    return this.http.post<void>(`${EQUIPES}/${equipeId}/membros`, { usuarioId });
  }

  desvincularMembro(equipeId: string, usuarioId: string): Observable<void> {
    return this.http.delete<void>(`${EQUIPES}/${equipeId}/membros/${usuarioId}`);
  }
}
