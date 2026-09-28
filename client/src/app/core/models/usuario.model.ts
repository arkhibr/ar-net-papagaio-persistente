/** GET /api/v1/usuarios (só Supervisor). */
export interface UsuarioDoDiretorioDto {
  id: string;
  nome: string | null;
  login: string | null;
  papeis: string[];
}

/** GET /api/v1/usuarios/nomes. */
export interface NomeDeUsuarioDto {
  id: string;
  nome: string | null;
}
