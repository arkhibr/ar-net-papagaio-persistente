import { Component, computed, input } from '@angular/core';
import { StatusChamado } from '../../../core/models/chamado.model';

@Component({
  selector: 'app-status-chamado-badge',
  standalone: true,
  template: `<span class="badge" [class]="corClasse()">{{ rotulo() }}</span>`,
  styleUrl: './status-chamado-badge.component.scss',
})
export class StatusChamadoBadgeComponent {
  status = input.required<StatusChamado>();

  private readonly rotulos: Record<StatusChamado, string> = {
    [StatusChamado.Aberto]: 'Aberto',
    [StatusChamado.EmAtendimento]: 'Em atendimento',
    [StatusChamado.Resolvido]: 'Resolvido',
    [StatusChamado.Fechado]: 'Fechado',
  };

  private readonly cores: Record<StatusChamado, string> = {
    [StatusChamado.Aberto]: 'cor-aberto',
    [StatusChamado.EmAtendimento]: 'cor-em-atendimento',
    [StatusChamado.Resolvido]: 'cor-resolvido',
    [StatusChamado.Fechado]: 'cor-fechado',
  };

  rotulo = computed(() => this.rotulos[this.status()]);
  corClasse = computed(() => this.cores[this.status()]);
}
