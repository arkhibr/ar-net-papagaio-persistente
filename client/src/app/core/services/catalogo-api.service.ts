import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CategoriaDeServicoDto } from '../models/catalogo.model';

@Injectable({ providedIn: 'root' })
export class CatalogoApiService {
  constructor(private readonly http: HttpClient) {}

  listarCategorias(): Observable<CategoriaDeServicoDto[]> {
    return this.http.get<CategoriaDeServicoDto[]>('/api/v1/categorias-de-servico');
  }
}
