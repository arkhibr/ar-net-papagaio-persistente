import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';

export interface Persona {
  apelido: string;
  userId: string;
  roles: string[];
}

export interface UsuarioAtual {
  userId: string;
  roles: string[];
}

const CHAVE_SESSAO = 'currentUser';
const CHAVE_PERSONAS = 'personas';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly _currentUser = signal<UsuarioAtual | null>(this.lerSessaoArmazenada());
  readonly currentUser = this._currentUser.asReadonly();

  constructor(private readonly http: HttpClient) {}

  hasRole(role: string): boolean {
    return this._currentUser()?.roles.includes(role) ?? false;
  }

  login(userId: string, roles: string[]): Observable<void> {
    return this.http.post<{ userId: string; roles: string[] }>('/dev/login', { userId, roles }).pipe(
      tap(() => {
        const usuario: UsuarioAtual = { userId, roles };
        this._currentUser.set(usuario);
        sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify(usuario));
      }),
      map(() => undefined),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/dev/logout', {}).pipe(
      tap(() => {
        this._currentUser.set(null);
        sessionStorage.removeItem(CHAVE_SESSAO);
      }),
    );
  }

  listarPersonas(): Persona[] {
    const bruto = localStorage.getItem(CHAVE_PERSONAS);
    return bruto ? (JSON.parse(bruto) as Persona[]) : [];
  }

  salvarPersona(persona: Persona): void {
    const personas = this.listarPersonas().filter((p) => p.userId !== persona.userId);
    personas.push(persona);
    localStorage.setItem(CHAVE_PERSONAS, JSON.stringify(personas));
  }

  removerPersona(userId: string): void {
    const personas = this.listarPersonas().filter((p) => p.userId !== userId);
    localStorage.setItem(CHAVE_PERSONAS, JSON.stringify(personas));
  }

  private lerSessaoArmazenada(): UsuarioAtual | null {
    const bruto = sessionStorage.getItem(CHAVE_SESSAO);
    return bruto ? (JSON.parse(bruto) as UsuarioAtual) : null;
  }
}
