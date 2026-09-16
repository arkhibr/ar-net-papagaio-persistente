import { Component, OnInit, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CatalogoApiService } from '../../../core/services/catalogo-api.service';
import { CategoriaDeServicoDto } from '../../../core/models/catalogo.model';

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [MatTableModule, MatProgressSpinnerModule],
  templateUrl: './categorias.component.html',
  styleUrl: './categorias.component.scss',
})
export class CategoriasComponent implements OnInit {
  private readonly catalogoApi = inject(CatalogoApiService);

  readonly categorias = signal<CategoriaDeServicoDto[]>([]);
  readonly carregando = signal(true);
  readonly colunas = ['nome', 'equipeId'];

  ngOnInit(): void {
    this.catalogoApi.listarCategorias().subscribe({
      next: (categorias) => {
        this.categorias.set(categorias);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }
}
