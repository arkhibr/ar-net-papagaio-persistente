import { Injectable, computed, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';
import { SEM_TRATAMENTO_GLOBAL_DE_ERRO } from '../http/contexto';

/** Corpo de GET /api/me (contratos-front.md, seção 2). Só para UX, nunca autorização. */
export interface UsuarioAtual {
  userId: string;
  nome: string | null;
  papeis: string[];
}

/**
 * Sessão vista pelo Angular (arquitetura/17, "Angular como cliente fino"; adaptacao-bff-angular.md,
 * F1). A identidade e os papéis vêm do backend em GET /api/me; nada fica no armazenamento do
 * navegador (arquitetura/11). O cookie de sessão é HttpOnly e o Angular nunca o lê.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly _currentUser = signal<UsuarioAtual | null>(null);
  private readonly _carregado = signal(false);

  readonly currentUser = this._currentUser.asReadonly();
  readonly carregado = this._carregado.asReadonly();
  readonly autenticado = computed(() => this._currentUser() !== null);

  constructor(private readonly http: HttpClient) {}

  /**
   * Chamado no bootstrap (provideAppInitializer). 200 preenche o usuário; 401 significa sem
   * sessão. A resposta também traz o cookie XSRF-TOKEN da identidade atual (B1/B2).
   */
  carregar(): Observable<UsuarioAtual | null> {
    return this.http
      .get<UsuarioAtual>('/api/me', { context: new HttpContext().set(SEM_TRATAMENTO_GLOBAL_DE_ERRO, true) })
      .pipe(
        catchError((erro: HttpErrorResponse) => (erro.status === 401 ? of(null) : throwError(() => erro))),
        tap((usuario) => {
          this._currentUser.set(usuario);
          this._carregado.set(true);
        }),
      );
  }

  hasRole(papel: string): boolean {
    return this._currentUser()?.papeis.includes(papel) ?? false;
  }

  /** POST /auth/logout (com XSRF automático) e devolve para onde navegar. */
  logout(): Observable<string> {
    return this.http.post<{ redirectUrl: string | null }>('/auth/logout', null).pipe(
      tap(() => this.limpar()),
      map((resposta) => resposta.redirectUrl ?? '/auth/login'),
    );
  }

  limpar(): void {
    this._currentUser.set(null);
  }
}
