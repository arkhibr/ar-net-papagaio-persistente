import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { ChamadosApiService } from '../../../core/services/chamados-api.service';
import { SessionService } from '../../../core/services/session.service';
import { DiretorioDeUsuariosService } from '../../../core/services/diretorio-de-usuarios.service';
import { ChamadoDetalheDto, PrioridadeChamado, StatusChamado } from '../../../core/models/chamado.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { ChaveDeIdempotencia } from '../../../core/http/chave-de-idempotencia';
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
  protected readonly diretorio = inject(DiretorioDeUsuariosService);
  private readonly dialog = inject(MatDialog);

  readonly chamado = signal<ChamadoDetalheDto | null>(null);
  private etag: string | null = null;
  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly StatusChamado = StatusChamado;
  readonly emAndamento = signal(false);

  private readonly chaves: Record<AcaoDoChamado, ChaveDeIdempotencia> = {
    atribuir: new ChaveDeIdempotencia(),
    devolver: new ChaveDeIdempotencia(),
    reclassificar: new ChaveDeIdempotencia(),
    resolver: new ChaveDeIdempotencia(),
    fechar: new ChaveDeIdempotencia(),
    reabrir: new ChaveDeIdempotencia(),
  };

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
      next: ({ chamado, etag }) => {
        this.chamado.set(chamado);
        this.diretorio.resolver([chamado.solicitanteId, chamado.tecnicoAtribuidoId]);
        this.etag = etag;
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
        if (!this.etag) {
          this.carregar();
          return;
        }
        const etag = this.etag;
        this.executar('atribuir', { id: c.id, etag }, (chave) => this.chamadosApi.atribuir(c.id, etag, chave), (problema) => {
          // 412/409: o chamado mudou desde o GET; recarrega para o usuário decidir de novo.
          if (problema.status === 412 || problema.status === 409) {
            this.carregar();
          }
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
        const id = this.chamado()!.id;
        this.executar('reclassificar', { id, novaPrioridade }, (chave) =>
          this.chamadosApi.reclassificar(id, novaPrioridade, chave),
        );
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
        const id = this.chamado()!.id;
        this.executar('resolver', { id, notaResolucao }, (chave) => this.chamadosApi.resolver(id, notaResolucao, chave));
      });
  }

  devolver(): void {
    this.confirmarEExecutar('devolver', 'Devolver à fila', 'Confirma devolver este chamado para a fila da equipe?', (id, chave) =>
      this.chamadosApi.devolver(id, chave),
    );
  }

  fechar(): void {
    this.confirmarEExecutar('fechar', 'Fechar chamado', 'Confirma o fechamento deste chamado?', (id, chave) =>
      this.chamadosApi.fechar(id, chave),
    );
  }

  reabrir(): void {
    this.confirmarEExecutar('reabrir', 'Reabrir chamado', 'Confirma reabrir este chamado?', (id, chave) =>
      this.chamadosApi.reabrir(id, chave),
    );
  }

  private confirmarEExecutar(
    acao: AcaoDoChamado,
    titulo: string,
    mensagem: string,
    chamada: (id: string, chave: string) => Observable<void>,
  ): void {
    this.dialog
      .open(ConfirmDialogComponent, { data: { titulo, mensagem } })
      .afterClosed()
      .subscribe((confirmado: boolean | undefined) => {
        if (confirmado) {
          const id = this.chamado()!.id;
          this.executar(acao, { id }, (chave) => chamada(id, chave));
        }
      });
  }

  /**
   * Uma Idempotency-Key por ação (F6): a mesma chave nos reenvios com o mesmo conteúdo, uma nova
   * depois de sucesso. Os botões ficam desabilitados enquanto a requisição está em andamento.
   */
  private executar(
    acao: AcaoDoChamado,
    conteudo: unknown,
    chamada: (chave: string) => Observable<void>,
    aoFalhar?: (problema: ProblemDetails) => void,
  ): void {
    if (this.emAndamento()) {
      return;
    }
    this.emAndamento.set(true);
    const chave = this.chaves[acao];
    chamada(chave.para(conteudo)).subscribe({
      next: () => {
        chave.concluir();
        this.emAndamento.set(false);
        this.carregar();
      },
      error: (problema: ProblemDetails) => {
        this.emAndamento.set(false);
        aoFalhar?.(problema);
      },
    });
  }
}

type AcaoDoChamado = 'atribuir' | 'devolver' | 'reclassificar' | 'resolver' | 'fechar' | 'reabrir';
