import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { Observable } from 'rxjs';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { mensagemDeErro } from '../../util/mensagem-de-erro';

export interface NomeDialogData {
  titulo: string;
  valor: string;
  tamanhoMaximo: number;
  /** Chamada feita ao salvar; o diálogo só fecha (com true) se ela der certo. */
  salvar: (nome: string) => Observable<unknown>;
}

/** Diálogo de um campo "Nome", para criar ou renomear. Mostra o erro da API no próprio diálogo. */
@Component({
  selector: 'app-nome-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.titulo }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="campo">
        <mat-label>Nome</mat-label>
        <input matInput [formControl]="nome" [maxlength]="data.tamanhoMaximo" (keydown.enter)="salvar()" />
      </mat-form-field>
      @if (erro()) {
        <p class="erro">{{ erro() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">Cancelar</button>
      <button mat-raised-button color="primary" [disabled]="nome.invalid || salvando()" (click)="salvar()">Salvar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .campo { width: min(400px, 75vw); }
    .erro { color: #d32f2f; }
  `,
})
export class NomeDialogComponent {
  protected readonly data = inject<NomeDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<NomeDialogComponent>);

  readonly nome = new FormControl(this.data.valor, {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(this.data.tamanhoMaximo)],
  });
  readonly salvando = signal(false);
  readonly erro = signal<string | null>(null);

  salvar(): void {
    const nome = this.nome.value.trim();
    if (this.nome.invalid || this.salvando() || nome.length === 0) {
      return;
    }
    if (nome === this.data.valor) {
      this.dialogRef.close(false);
      return;
    }
    this.salvando.set(true);
    this.erro.set(null);
    this.data.salvar(nome).subscribe({
      next: () => this.dialogRef.close(true),
      error: (problema: ProblemDetails) => {
        this.salvando.set(false);
        this.erro.set(mensagemDeErro(problema, 'Não foi possível salvar.'));
      },
    });
  }
}
