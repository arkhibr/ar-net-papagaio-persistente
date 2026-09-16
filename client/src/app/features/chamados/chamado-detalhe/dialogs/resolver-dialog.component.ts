import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-resolver-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './resolver-dialog.component.html',
})
export class ResolverDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ResolverDialogComponent>);

  readonly form = this.fb.nonNullable.group({
    notaResolucao: ['', Validators.required],
  });

  confirmar(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.getRawValue().notaResolucao);
    }
  }
}
