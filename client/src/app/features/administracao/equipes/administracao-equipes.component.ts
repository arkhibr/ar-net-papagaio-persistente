import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, forkJoin } from 'rxjs';
import { CatalogoApiService } from '../../../core/services/catalogo-api.service';
import { DiretorioDeUsuariosService } from '../../../core/services/diretorio-de-usuarios.service';
import { EquipeDto } from '../../../core/models/catalogo.model';
import { UsuarioDoDiretorioDto } from '../../../core/models/usuario.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { NomeDialogComponent, NomeDialogData } from '../../../shared/components/nome-dialog/nome-dialog.component';
import { mensagemDeErro } from '../../../shared/util/mensagem-de-erro';

const TAMANHO_MAXIMO_DO_NOME = 100;

/**
 * Administração de equipes e membros (Supervisor; contratos-front.md, seção 7.12). Só técnicos e
 * supervisores sem equipe aparecem para vincular: cada pessoa fica em no máximo uma equipe.
 */
@Component({
  selector: 'app-administracao-equipes',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule, MatProgressSpinnerModule],
  template: `
    <div class="cabecalho">
      <h1>Equipes</h1>
      <button mat-raised-button color="primary" (click)="criar()">Nova equipe</button>
    </div>

    @if (carregando()) {
      <mat-spinner diameter="32" />
    } @else {
      @for (equipe of equipes(); track equipe.id) {
        <section class="surface-card equipe">
          <div class="cabecalho">
            <h2>{{ equipe.nome }}</h2>
            <button mat-stroked-button (click)="renomear(equipe)">Renomear</button>
          </div>

          @if (equipe.membros.length === 0) {
            <p class="vazio">Nenhum membro.</p>
          } @else {
            <ul class="membros">
              @for (membro of equipe.membros; track membro) {
                <li>
                  <span>{{ diretorio.nome(membro) }} <small>{{ descricao(membro) }}</small></span>
                  <button mat-button [disabled]="emAndamento()" (click)="desvincular(equipe, membro)">Remover</button>
                </li>
              }
            </ul>
          }

          <div class="adicionar">
            <mat-form-field appearance="outline">
              <mat-label>Adicionar membro</mat-label>
              <mat-select [(ngModel)]="selecionado[equipe.id]" [disabled]="candidatos().length === 0">
                @for (usuario of candidatos(); track usuario.id) {
                  <mat-option [value]="usuario.id">{{ rotulo(usuario) }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <button mat-stroked-button [disabled]="!selecionado[equipe.id] || emAndamento()" (click)="vincular(equipe)">
              Adicionar
            </button>
          </div>
        </section>
      } @empty {
        <p class="vazio">Nenhuma equipe cadastrada.</p>
      }
      @if (candidatos().length === 0 && equipes().length > 0) {
        <p class="vazio">Todos os técnicos e supervisores já estão em alguma equipe.</p>
      }
    }
  `,
  styles: `
    .cabecalho { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
    .equipe { margin-bottom: var(--spacing-lg); }
    h2 { margin: 0; font-size: 18px; }
    .membros { list-style: none; padding: 0; margin: var(--spacing-sm) 0; }
    .membros li { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 4px 0; border-bottom: 1px solid var(--color-divider-soft); }
    small { color: var(--color-ink-muted-48); margin-left: 6px; }
    .adicionar { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; margin-top: var(--spacing-sm); }
    .adicionar mat-form-field { flex: 1 1 240px; }
    .vazio { color: var(--color-ink-muted-48); }
  `,
})
export class AdministracaoEquipesComponent implements OnInit {
  private readonly catalogoApi = inject(CatalogoApiService);
  protected readonly diretorio = inject(DiretorioDeUsuariosService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly equipes = signal<EquipeDto[]>([]);
  readonly usuarios = signal<UsuarioDoDiretorioDto[]>([]);
  readonly carregando = signal(true);
  readonly emAndamento = signal(false);
  readonly selecionado: Record<string, string | undefined> = {};

  /** Técnicos e supervisores que ainda não estão em nenhuma equipe. */
  readonly candidatos = computed(() => {
    const membros = new Set(this.equipes().flatMap((e) => e.membros));
    return this.usuarios().filter(
      (u) => !membros.has(u.id) && (u.papeis.includes('Tecnico') || u.papeis.includes('Supervisor')),
    );
  });

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    forkJoin({ equipes: this.catalogoApi.listarEquipes(), usuarios: this.diretorio.listar() }).subscribe({
      next: ({ equipes, usuarios }) => {
        this.equipes.set(equipes);
        this.usuarios.set(usuarios);
        this.diretorio.resolver(equipes.flatMap((e) => e.membros));
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  rotulo(usuario: UsuarioDoDiretorioDto): string {
    const nome = usuario.nome ?? usuario.login ?? `Usuário ${usuario.id.slice(0, 8)}`;
    return `${nome} (${usuario.papeis.join(', ')})`;
  }

  descricao(usuarioId: string): string {
    const usuario = this.usuarios().find((u) => u.id === usuarioId);
    return usuario ? [usuario.login, usuario.papeis.join(', ')].filter(Boolean).join(' · ') : '';
  }

  criar(): void {
    this.abrirNome({
      titulo: 'Nova equipe',
      valor: '',
      tamanhoMaximo: TAMANHO_MAXIMO_DO_NOME,
      salvar: (nome) => this.catalogoApi.criarEquipe(nome),
    });
  }

  renomear(equipe: EquipeDto): void {
    this.abrirNome({
      titulo: 'Renomear equipe',
      valor: equipe.nome,
      tamanhoMaximo: TAMANHO_MAXIMO_DO_NOME,
      salvar: (nome) => this.catalogoApi.renomearEquipe(equipe.id, nome),
    });
  }

  vincular(equipe: EquipeDto): void {
    const usuarioId = this.selecionado[equipe.id];
    if (!usuarioId) {
      return;
    }
    this.executar(this.catalogoApi.vincularMembro(equipe.id, usuarioId), 'Membro adicionado.', () => {
      this.selecionado[equipe.id] = undefined;
    });
  }

  desvincular(equipe: EquipeDto, usuarioId: string): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          titulo: 'Remover membro',
          mensagem: `Remover ${this.diretorio.nome(usuarioId)} de "${equipe.nome}"? A pessoa deixa de ver a fila desta equipe.`,
        },
      })
      .afterClosed()
      .subscribe((confirmado: boolean | undefined) => {
        if (confirmado) {
          this.executar(this.catalogoApi.desvincularMembro(equipe.id, usuarioId), 'Membro removido.');
        }
      });
  }

  private abrirNome(data: NomeDialogData): void {
    this.dialog
      .open(NomeDialogComponent, { data })
      .afterClosed()
      .subscribe((salvou: boolean | undefined) => {
        if (salvou) {
          this.carregar();
        }
      });
  }

  private executar(acao: Observable<void>, sucesso: string, depois?: () => void): void {
    this.emAndamento.set(true);
    acao.subscribe({
      next: () => {
        this.emAndamento.set(false);
        depois?.();
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
