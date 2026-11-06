import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { ChamadosApiService } from '../../../core/services/chamados-api.service';
import { CatalogoApiService } from '../../../core/services/catalogo-api.service';
import { CategoriaDeServicoDto } from '../../../core/models/catalogo.model';
import { PrioridadeChamado } from '../../../core/models/chamado.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { ChaveDeIdempotencia } from '../../../core/http/chave-de-idempotencia';

const OPCOES_PRIORIDADE = [
  { valor: PrioridadeChamado.Baixa, rotulo: 'Baixa' },
  { valor: PrioridadeChamado.Media, rotulo: 'Média' },
  { valor: PrioridadeChamado.Alta, rotulo: 'Alta' },
  { valor: PrioridadeChamado.Critica, rotulo: 'Crítica' },
];

@Component({
  selector: 'app-abrir-chamado',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  templateUrl: './abrir-chamado.component.html',
  styleUrl: './abrir-chamado.component.scss',
})
export class AbrirChamadoComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly chamadosApi = inject(ChamadosApiService);
  private readonly catalogoApi = inject(CatalogoApiService);
  private readonly router = inject(Router);

  readonly opcoesPrioridade = OPCOES_PRIORIDADE;
  readonly categorias = signal<CategoriaDeServicoDto[]>([]);
  readonly enviando = signal(false);
  readonly erroGeral = signal<string | null>(null);

  /** Criada ao abrir o formulário; reutilizada em nova tentativa com o mesmo conteúdo (F6). */
  private readonly chave = new ChaveDeIdempotencia();

  readonly form = this.fb.nonNullable.group({
    categoriaId: ['', Validators.required],
    prioridade: [PrioridadeChamado.Media, Validators.required],
  });

  ngOnInit(): void {
    this.catalogoApi.listarCategorias().subscribe((categorias) => this.categorias.set(categorias));
  }

  enviar(): void {
    if (this.form.invalid || this.enviando()) {
      return;
    }
    this.erroGeral.set(null);
    this.enviando.set(true);
    const { categoriaId, prioridade } = this.form.getRawValue();
    this.chamadosApi.abrir(categoriaId, prioridade, this.chave.para({ categoriaId, prioridade })).subscribe({
      next: ({ id }) => {
        this.chave.concluir();
        this.router.navigate(['/chamados', id]);
      },
      error: (problema: ProblemDetails) => {
        this.enviando.set(false);
        this.erroGeral.set(
          problema.errors?.map((e) => e.mensagem).join(' ') ?? problema.detail ?? 'Não foi possível abrir o chamado.',
        );
      },
    });
  }
}
