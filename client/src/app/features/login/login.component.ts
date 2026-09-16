import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatListModule } from '@angular/material/list';
import { Persona, SessionService } from '../../core/services/session.service';

const PAPEIS_DISPONIVEIS = ['Solicitante', 'Tecnico', 'Supervisor'];

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatCheckboxModule,
    MatListModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly papeisDisponiveis = PAPEIS_DISPONIVEIS;
  readonly personas = signal<Persona[]>(this.session.listarPersonas());

  readonly apelido = signal('');
  readonly userId = signal('');
  readonly papeisSelecionados = signal<Set<string>>(new Set());
  readonly erro = signal<string | null>(null);

  gerarUserId(): void {
    this.userId.set(crypto.randomUUID());
  }

  alternarPapel(papel: string, marcado: boolean): void {
    const atual = new Set(this.papeisSelecionados());
    if (marcado) {
      atual.add(papel);
    } else {
      atual.delete(papel);
    }
    this.papeisSelecionados.set(atual);
  }

  criarPersonaEEntrar(): void {
    const persona: Persona = {
      apelido: this.apelido(),
      userId: this.userId(),
      roles: Array.from(this.papeisSelecionados()),
    };
    this.session.salvarPersona(persona);
    this.personas.set(this.session.listarPersonas());
    this.entrarComo(persona);
  }

  entrarComo(persona: Persona): void {
    this.erro.set(null);
    this.session.login(persona.userId, persona.roles).subscribe({
      next: () => {
        const redirectTo = this.route.snapshot.queryParamMap.get('redirectTo') ?? '/chamados/meus';
        this.router.navigateByUrl(redirectTo);
      },
      error: () =>
        this.erro.set('Não foi possível entrar. Confirme se a Api está rodando em modo Development.'),
    });
  }

  removerPersona(userId: string): void {
    this.session.removerPersona(userId);
    this.personas.set(this.session.listarPersonas());
  }
}
