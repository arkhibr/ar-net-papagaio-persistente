import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { PrioridadeChamado } from '../../../../core/models/chamado.model';

const OPCOES_PRIORIDADE = [
  { valor: PrioridadeChamado.Baixa, rotulo: 'Baixa' },
  { valor: PrioridadeChamado.Media, rotulo: 'Média' },
  { valor: PrioridadeChamado.Alta, rotulo: 'Alta' },
  { valor: PrioridadeChamado.Critica, rotulo: 'Crítica' },
];

@Component({
  selector: 'app-reclassificar-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  templateUrl: './reclassificar-dialog.component.html',
})
export class ReclassificarDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ReclassificarDialogComponent>);

  readonly opcoesPrioridade = OPCOES_PRIORIDADE;

  readonly form = this.fb.nonNullable.group({
    novaPrioridade: [PrioridadeChamado.Media, Validators.required],
  });

  confirmar(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.getRawValue().novaPrioridade);
    }
  }
}
