import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { Observable, concat } from 'rxjs';
import { CatalogoApiService } from '../../../core/services/catalogo-api.service';
import { CategoriaDeServicoDto, EquipeDto, SlaDto } from '../../../core/models/catalogo.model';
import { ProblemDetails } from '../../../core/models/problem-details.model';
import { PRIORIDADES, mensagemDeErro } from '../../../shared/util/mensagem-de-erro';

export interface CategoriaDialogData {
  categoria: CategoriaDeServicoDto | null;
  equipes: EquipeDto[];
}

/**
 * Criar ou editar categoria. Na edição, só as ações que mudaram são enviadas (renomear,
 * transferir, definir-slas), uma depois da outra: se uma falhar, as anteriores já valeram, e a
 * lista é recarregada ao fechar. Hora de SLA em branco = a categoria não atende a prioridade.
 */
@Component({
  selector: 'app-categoria-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.categoria ? 'Editar categoria' : 'Nova categoria' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="formulario">
        <mat-form-field appearance="outline">
          <mat-label>Nome</mat-label>
          <input matInput formControlName="nome" maxlength="200" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Equipe responsável</mat-label>
          <mat-select formControlName="equipeId">
            @for (equipe of data.equipes; track equipe.id) {
              <mat-option [value]="equipe.id">{{ equipe.nome }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <p class="legenda">SLA em horas por prioridade. Em branco: a categoria não atende a prioridade.</p>
        <div class="slas" formGroupName="slas">
          @for (p of prioridades; track p.valor) {
            <mat-form-field appearance="outline">
              <mat-label>{{ p.rotulo }}</mat-label>
              <input matInput type="number" min="1" [formControlName]="p.valor.toString()" />
            </mat-form-field>
          }
        </div>
        @if (erro()) {
          <p class="erro">{{ erro() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="alterou">Cancelar</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid || salvando()" (click)="salvar()">Salvar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .formulario { display: flex; flex-direction: column; min-width: min(480px, 80vw); }
    .slas { display: grid; grid-template-columns: repeat(auto-fit, minmax(100px, 1fr)); gap: 8px; }
    .legenda { margin: 0 0 8px; font-size: 13px; color: var(--color-ink-muted-48); }
    .erro { color: #d32f2f; }
  `,
})
export class CategoriaDialogComponent {
  protected readonly data = inject<CategoriaDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CategoriaDialogComponent>);
  private readonly catalogoApi = inject(CatalogoApiService);
  private readonly fb = inject(FormBuilder);

  readonly prioridades = PRIORIDADES;
  readonly salvando = signal(false);
  readonly erro = signal<string | null>(null);
  /** true se alguma ação já valeu, para a lista recarregar mesmo se o usuário cancelar depois de um erro. */
  alterou = false;

  readonly form = this.fb.group({
    nome: [this.data.categoria?.nome ?? '', [Validators.required, Validators.maxLength(200)]],
    equipeId: [this.data.categoria?.equipeId ?? '', Validators.required],
    slas: this.fb.group(
      Object.fromEntries(
        PRIORIDADES.map((p) => [
          p.valor.toString(),
          [this.data.categoria?.slas.find((s) => s.prioridade === p.valor)?.horas ?? null, Validators.min(1)],
        ]),
      ),
    ),
  });

  salvar(): void {
    const valor = this.form.getRawValue();
    const nome = (valor.nome ?? '').trim();
    const equipeId = valor.equipeId ?? '';
    const slas = this.slasDoFormulario(valor.slas as Record<string, number | null>);
    const atual = this.data.categoria;

    const acoes: Observable<unknown>[] = [];
    if (!atual) {
      acoes.push(this.catalogoApi.criarCategoria(nome, equipeId, slas));
    } else {
      if (nome !== atual.nome) {
        acoes.push(this.catalogoApi.renomearCategoria(atual.id, nome));
      }
      if (equipeId !== atual.equipeId) {
        acoes.push(this.catalogoApi.transferirCategoria(atual.id, equipeId));
      }
      if (JSON.stringify(slas) !== JSON.stringify([...atual.slas].sort((a, b) => a.prioridade - b.prioridade))) {
        acoes.push(this.catalogoApi.definirSlas(atual.id, slas));
      }
    }

    if (acoes.length === 0) {
      this.dialogRef.close(false);
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);
    concat(...acoes).subscribe({
      next: () => (this.alterou = true),
      complete: () => this.dialogRef.close(true),
      error: (problema: ProblemDetails) => {
        this.salvando.set(false);
        this.erro.set(mensagemDeErro(problema, 'Não foi possível salvar a categoria.'));
      },
    });
  }

  private slasDoFormulario(valores: Record<string, number | null>): SlaDto[] {
    return PRIORIDADES.filter((p) => valores[p.valor.toString()] != null && `${valores[p.valor.toString()]}` !== '').map(
      (p) => ({ prioridade: p.valor, horas: Number(valores[p.valor.toString()]) }),
    );
  }
}
