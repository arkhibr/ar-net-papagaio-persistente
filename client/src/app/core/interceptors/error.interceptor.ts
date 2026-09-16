import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, catchError, throwError } from 'rxjs';
import { ProblemDetails } from '../models/problem-details.model';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((erro: HttpErrorResponse) => {
      if (erro.status === 401) {
        router.navigate(['/login']);
        return EMPTY;
      }

      if (erro.status === 403) {
        snackBar.open('Acesso negado: você não tem permissão para esta ação.', 'Fechar', { duration: 5000 });
      } else if (erro.status === 409) {
        snackBar.open('Conflito: o recurso foi alterado ou a operação já está em andamento.', 'Fechar', {
          duration: 5000,
        });
      } else if (erro.status >= 500) {
        snackBar.open('Erro interno no servidor. Tente novamente.', 'Fechar', { duration: 5000 });
      }

      const problema = (erro.error as ProblemDetails) ?? {
        type: 'about:blank',
        title: 'Erro desconhecido',
        status: erro.status,
      };
      return throwError(() => problema);
    }),
  );
};
