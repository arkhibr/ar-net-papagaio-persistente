import { Component, OnInit, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { CatalogoApiService } from '../../../core/services/catalogo-api.service';
import { CategoriaDeServicoDto, EquipeDto } from '../../../core/models/catalogo.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PRIORIDADES, mensagemDeErro } from '../../../shared/util/mensagem-de-erro';
import { CategoriaDialogComponent, CategoriaDialogData } from './categoria-dialog.component';

/**
 * Administração de categorias (Supervisor; contratos-front.md, seção 7.12). Lista também as
 * inativas. Uma categoria nunca é apagada: inativar tira da abertura de chamado, e os chamados
 * antigos continuam.
 */
@Component({
  selector: 'app-administracao-categorias',
  standalone: true,
  imports: [MatTableModule, MatButtonModule, MatProgressSpinnerModule],
  template: `
    <div class="cabecalho">
      <h1>Categorias de serviço</h1>
      <button mat-raised-button color="primary" [disabled]="equipes().length === 0" (click)="editar(null)">Nova categoria</button>
    </div>
    @if (equipes().length === 0 && !carregando()) {
      <p class="aviso">Cadastre uma equipe antes de criar categorias.</p>
    }

    @if (carregando()) {
      <mat-spinner diameter="32" />
    } @else {
      <div class="surface-card sem-padding">
        <table mat-table [dataSource]="categorias()">
          <ng-container matColumnDef="nome">
            <th mat-header-cell *matHeaderCellDef>Nome</th>
            <td mat-cell *matCellDef="let c" [class.inativa]="!c.ativa">{{ c.nome }}</td>
          </ng-container>
          <ng-container matColumnDef="equipe">
            <th mat-header-cell *matHeaderCellDef>Equipe</th>
            <td mat-cell *matCellDef="let c">{{ c.equipeNome ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="slas">
            <th mat-header-cell *matHeaderCellDef>SLA (horas)</th>
            <td mat-cell *matCellDef="let c">{{ resumoDosSlas(c) }}</td>
          </ng-container>
          <ng-container matColumnDef="situacao">
            <th mat-header-cell *matHeaderCellDef>Situação</th>
            <td mat-cell *matCellDef="let c">{{ c.ativa ? 'Ativa' : 'Inativa' }}</td>
          </ng-container>
          <ng-container matColumnDef="acoes">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c" class="acoes">
              <button mat-stroked-button [disabled]="emAndamento()" (click)="editar(c)">Editar</button>
              @if (c.ativa) {
                <button mat-stroked-button [disabled]="emAndamento()" (click)="inativar(c)">Inativar</button>
              } @else {
                <button mat-stroked-button [disabled]="emAndamento()" (click)="reativar(c)">Reativar</button>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="colunas"></tr>
          <tr mat-row *matRowDef="let row; columns: colunas"></tr>
        </table>
        @if (categorias().length === 0) {
          <p class="vazio">Nenhuma categoria cadastrada.</p>
        }
      </div>
    }
  `,
  styles: `
    .cabecalho { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
    .sem-padding { padding: 0; overflow: hidden; }
    table { width: 100%; background: transparent; }
    .acoes { display: flex; gap: 8px; justify-content: flex-end; }
    .inativa { color: var(--color-ink-muted-48); text-decoration: line-through; }
    .vazio, .aviso { padding: var(--spacing-lg); color: var(--color-ink-muted-48); margin: 0; }
  `,
})
export class AdministracaoCategoriasComponent implements OnInit {
  private readonly catalogoApi = inject(CatalogoApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly colunas = ['nome', 'equipe', 'slas', 'situacao', 'acoes'];
  readonly categorias = signal<CategoriaDeServicoDto[]>([]);
  readonly equipes = signal<EquipeDto[]>([]);
  readonly carregando = signal(true);
  readonly emAndamento = signal(false);

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    forkJoin({
      categorias: this.catalogoApi.listarCategorias(true),
      equipes: this.catalogoApi.listarEquipes(),
    }).subscribe({
      next: ({ categorias, equipes }) => {
        this.categorias.set(categorias);
        this.equipes.set(equipes);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  resumoDosSlas(categoria: CategoriaDeServicoDto): string {
    if (categoria.slas.length === 0) {
      return 'nenhum';
    }
    return categoria.slas
      .map((s) => `${PRIORIDADES.find((p) => p.valor === s.prioridade)?.rotulo ?? s.prioridade} ${s.horas}`)
      .join(' · ');
  }

  editar(categoria: CategoriaDeServicoDto | null): void {
    const data: CategoriaDialogData = { categoria, equipes: this.equipes() };
    this.dialog
      .open(CategoriaDialogComponent, { data })
      .afterClosed()
      .subscribe((alterou: boolean | undefined) => {
        if (alterou) {
          this.carregar();
        }
      });
  }

  inativar(categoria: CategoriaDeServicoDto): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          titulo: 'Inativar categoria',
          mensagem: `"${categoria.nome}" deixa de aceitar chamados novos. Os chamados existentes continuam. Confirma?`,
        },
      })
      .afterClosed()
      .subscribe((confirmado: boolean | undefined) => {
        if (confirmado) {
          this.executar(this.catalogoApi.inativarCategoria(categoria.id), 'Categoria inativada.');
        }
      });
  }

  reativar(categoria: CategoriaDeServicoDto): void {
    this.executar(this.catalogoApi.reativarCategoria(categoria.id), 'Categoria reativada.');
  }

  private executar(acao: ReturnType<CatalogoApiService['inativarCategoria']>, sucesso: string): void {
    this.emAndamento.set(true);
    acao.subscribe({
      next: () => {
        this.emAndamento.set(false);
        this.snackBar.open(sucesso, 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (problema: ProblemDetails) => {
        this.emAndamento.set(false);
        if (problema.status === 400 || problema.status === 404) {
          this.snackBar.open(mensagemDeErro(problema, 'Não foi possível concluir a ação.'), 'Fechar', { duration: 5000 });
        }
      },
    });
  }
}
