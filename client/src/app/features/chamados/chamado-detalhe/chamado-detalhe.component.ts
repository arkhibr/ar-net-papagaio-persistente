import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { ChamadosApiService } from '../../../core/services/chamados-api.service';
import { SessionService } from '../../../core/services/session.service';
import { ChamadoDetalheDto, PrioridadeChamado, StatusChamado } from '../../../core/models/chamado.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { StatusChamadoBadgeComponent } from '../../../shared/components/status-chamado-badge/status-chamado-badge.component';
import { PrioridadeBadgeComponent } from '../../../shared/components/prioridade-badge/prioridade-badge.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ReclassificarDialogComponent } from './dialogs/reclassificar-dialog.component';
import { ResolverDialogComponent } from './dialogs/resolver-dialog.component';

@Component({
  selector: 'app-chamado-detalhe',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatProgressSpinnerModule,
    StatusChamadoBadgeComponent,
    PrioridadeBadgeComponent,
  ],
  templateUrl: './chamado-detalhe.component.html',
  styleUrl: './chamado-detalhe.component.scss',
})
export class ChamadoDetalheComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  protected readonly chamadosApi = inject(ChamadosApiService);
  protected readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);

  readonly chamado = signal<ChamadoDetalheDto | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly StatusChamado = StatusChamado;

  private get userId(): string | undefined {
    return this.session.currentUser()?.userId;
  }

  readonly podeAtribuir = computed(() => {
    const c = this.chamado();
    return !!c && c.status === StatusChamado.Aberto && this.session.hasRole('Tecnico');
  });

  readonly podeDevolver = computed(() => {
    const c = this.chamado();
    return !!c && c.status === StatusChamado.EmAtendimento && c.tecnicoAtribuidoId === this.userId;
  });

  readonly podeReclassificar = computed(() => {
    const c = this.chamado();
    return (
      !!c &&
      (c.status === StatusChamado.Aberto || c.status === StatusChamado.EmAtendimento) &&
      this.session.hasRole('Tecnico')
    );
  });

  readonly podeResolver = computed(() => {
    const c = this.chamado();
    return !!c && c.status === StatusChamado.EmAtendimento && c.tecnicoAtribuidoId === this.userId;
  });

  readonly podeFechar = computed(() => {
    const c = this.chamado();
    return !!c && c.status === StatusChamado.Resolvido && c.solicitanteId === this.userId;
  });

  readonly podeReabrir = computed(() => {
    const c = this.chamado();
    return !!c && c.status === StatusChamado.Fechado && c.solicitanteId === this.userId;
  });

  readonly dentroDoPrazoDeReabertura = computed(() => {
    const c = this.chamado();
    if (!c?.fechadoEm) {
      return true;
    }
    const prazo = new Date(c.fechadoEm);
    prazo.setDate(prazo.getDate() + 5);
    return new Date() <= prazo;
  });

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.carregando.set(true);
    this.erro.set(null);
    this.chamadosApi.obter(id).subscribe({
      next: (chamado) => {
        this.chamado.set(chamado);
        this.carregando.set(false);
      },
      error: (problema: ProblemDetails) => {
        this.erro.set(problema.detail ?? 'Não foi possível carregar o chamado.');
        this.carregando.set(false);
      },
    });
  }

  atribuir(): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: { titulo: 'Atribuir a mim', mensagem: 'Confirma atribuir este chamado a você?' },
      })
      .afterClosed()
      .subscribe((confirmado: boolean | undefined) => {
        if (!confirmado) {
          return;
        }
        const c = this.chamado()!;
        const tecnicoId = this.session.currentUser()!.userId;
        this.chamadosApi.atribuir(c.id, tecnicoId, c.rowVersion).subscribe({
          next: () => this.carregar(),
          error: (problema: ProblemDetails) => {
            if (problema.status === 409) {
              this.carregar();
            }
          },
        });
      });
  }

  reclassificar(): void {
    this.dialog
      .open(ReclassificarDialogComponent)
      .afterClosed()
      .subscribe((novaPrioridade: PrioridadeChamado | undefined) => {
        if (novaPrioridade === undefined) {
          return;
        }
        this.chamadosApi.reclassificar(this.chamado()!.id, novaPrioridade).subscribe({
          next: () => this.carregar(),
        });
      });
  }

  resolver(): void {
    this.dialog
      .open(ResolverDialogComponent)
      .afterClosed()
      .subscribe((notaResolucao: string | undefined) => {
        if (!notaResolucao) {
          return;
        }
        this.chamadosApi.resolver(this.chamado()!.id, notaResolucao).subscribe({
          next: () => this.carregar(),
        });
      });
  }

  devolver(): void {
    this.confirmarEExecutar(
      'Devolver à fila',
      'Confirma devolver este chamado para a fila da equipe?',
      () => this.chamadosApi.devolver(this.chamado()!.id),
    );
  }

  fechar(): void {
    this.confirmarEExecutar('Fechar chamado', 'Confirma o fechamento deste chamado?', () =>
      this.chamadosApi.fechar(this.chamado()!.id),
    );
  }

  reabrir(): void {
    this.confirmarEExecutar('Reabrir chamado', 'Confirma reabrir este chamado?', () =>
      this.chamadosApi.reabrir(this.chamado()!.id),
    );
  }

  private confirmarEExecutar(titulo: string, mensagem: string, acao: () => Observable<void>): void {
    this.dialog
      .open(ConfirmDialogComponent, { data: { titulo, mensagem } })
      .afterClosed()
      .subscribe((confirmado: boolean | undefined) => {
        if (confirmado) {
          acao().subscribe({ next: () => this.carregar() });
        }
      });
  }
}
