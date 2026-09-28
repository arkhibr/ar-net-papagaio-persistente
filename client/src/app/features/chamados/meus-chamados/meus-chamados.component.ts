import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChamadosApiService } from '../../../core/services/chamados-api.service';
import { DiretorioDeUsuariosService } from '../../../core/services/diretorio-de-usuarios.service';
import { ChamadoResumoDto } from '../../../core/models/chamado.model';
import { StatusChamadoBadgeComponent } from '../../../shared/components/status-chamado-badge/status-chamado-badge.component';
import { PrioridadeBadgeComponent } from '../../../shared/components/prioridade-badge/prioridade-badge.component';

@Component({
  selector: 'app-meus-chamados',
  standalone: true,
  imports: [
    DatePipe,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    StatusChamadoBadgeComponent,
    PrioridadeBadgeComponent,
  ],
  templateUrl: './meus-chamados.component.html',
  styleUrl: './meus-chamados.component.scss',
})
export class MeusChamadosComponent implements OnInit {
  private readonly chamadosApi = inject(ChamadosApiService);
  protected readonly diretorio = inject(DiretorioDeUsuariosService);
  private readonly router = inject(Router);

  readonly colunas = ['prioridade', 'status', 'abertoEm', 'prazoSla', 'tecnicoAtribuidoId'];
  readonly itens = signal<ChamadoResumoDto[]>([]);
  readonly totalItems = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly carregando = signal(true);

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.chamadosApi.meus(this.page(), this.pageSize()).subscribe({
      next: (resposta) => {
        this.itens.set(resposta.items);
        this.diretorio.resolver(resposta.items.map((c) => c.tecnicoAtribuidoId));
        this.totalItems.set(resposta.totalItems);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  mudarPagina(evento: PageEvent): void {
    this.page.set(evento.pageIndex + 1);
    this.pageSize.set(evento.pageSize);
    this.carregar();
  }

  abrirDetalhe(chamado: ChamadoResumoDto): void {
    this.router.navigate(['/chamados', chamado.id]);
  }
}
