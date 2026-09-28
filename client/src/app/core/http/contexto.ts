import { HttpContextToken } from '@angular/common/http';

/**
 * Chamada que trata o próprio erro e não deve passar pelo tratamento global do errorInterceptor.
 * Usado no bootstrap (GET /api/me): um 401 ali só significa "sem sessão", e redirecionar antes de a
 * aplicação existir quebraria o bootstrap (adaptacao-bff-angular.md, F1).
 */
export const SEM_TRATAMENTO_GLOBAL_DE_ERRO = new HttpContextToken<boolean>(() => false);
