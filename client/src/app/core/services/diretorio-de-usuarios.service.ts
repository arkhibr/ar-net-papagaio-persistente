import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { NomeDeUsuarioDto, UsuarioDoDiretorioDto } from '../models/usuario.model';

const LOTE = 100;

/**
 * Troca GUID por nome nas telas (item 5). Guarda em memória os nomes já resolvidos, inclusive os
 * desconhecidos (null), para não perguntar de novo; o cache some ao recarregar a página. Nomes
 * são só UX: nunca decidem permissão.
 */
@Injectable({ providedIn: 'root' })
export class DiretorioDeUsuariosService {
  private readonly nomes = signal<ReadonlyMap<string, string | null>>(new Map());
  private readonly pedidos = new Set<string>();

  constructor(private readonly http: HttpClient) {}

  /** Nome de exibição; enquanto não chega (ou se não houver), um identificador curto. */
  nome(id: string | null | undefined): string {
    if (!id) {
      return '—';
    }
    return this.nomes().get(id) ?? `Usuário ${id.slice(0, 8)}`;
  }

  /** Busca os nomes que ainda não estão no cache, em lotes de até 100. */
  resolver(ids: (string | null | undefined)[]): void {
    const faltando = [...new Set(ids.filter((id): id is string => !!id))].filter(
      (id) => !this.nomes().has(id) && !this.pedidos.has(id),
    );
    for (let i = 0; i < faltando.length; i += LOTE) {
      const lote = faltando.slice(i, i + LOTE);
      lote.forEach((id) => this.pedidos.add(id));
      let params = new HttpParams();
      lote.forEach((id) => (params = params.append('ids', id)));
      this.http.get<NomeDeUsuarioDto[]>('/api/v1/usuarios/nomes', { params }).subscribe({
        next: (resposta) => this.guardar(resposta),
        error: () => lote.forEach((id) => this.pedidos.delete(id)),
      });
    }
  }

  /** Lista completa (só Supervisor), usada pela administração de equipes. Também alimenta o cache. */
  listar(): Observable<UsuarioDoDiretorioDto[]> {
    return this.http.get<UsuarioDoDiretorioDto[]>('/api/v1/usuarios').pipe(tap((usuarios) => this.guardar(usuarios)));
  }

  private guardar(itens: { id: string; nome: string | null }[]): void {
    const proximo = new Map(this.nomes());
    itens.forEach((item) => {
      proximo.set(item.id, item.nome);
      this.pedidos.delete(item.id);
    });
    this.nomes.set(proximo);
  }
}
