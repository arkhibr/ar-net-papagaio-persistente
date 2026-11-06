import { Component, computed, input } from '@angular/core';
import { PrioridadeChamado } from '../../../core/models/chamado.model';

@Component({
  selector: 'app-prioridade-badge',
  standalone: true,
  template: `<span class="badge" [class]="corClasse()">{{ rotulo() }}</span>`,
  styleUrl: './prioridade-badge.component.scss',
})
export class PrioridadeBadgeComponent {
  prioridade = input.required<PrioridadeChamado>();

  private readonly rotulos: Record<PrioridadeChamado, string> = {
    [PrioridadeChamado.Baixa]: 'Baixa',
    [PrioridadeChamado.Media]: 'Média',
    [PrioridadeChamado.Alta]: 'Alta',
    [PrioridadeChamado.Critica]: 'Crítica',
  };

  private readonly cores: Record<PrioridadeChamado, string> = {
    [PrioridadeChamado.Baixa]: 'cor-baixa',
    [PrioridadeChamado.Media]: 'cor-media',
    [PrioridadeChamado.Alta]: 'cor-alta',
    [PrioridadeChamado.Critica]: 'cor-critica',
  };

  rotulo = computed(() => this.rotulos[this.prioridade()]);
  corClasse = computed(() => this.cores[this.prioridade()]);
}
