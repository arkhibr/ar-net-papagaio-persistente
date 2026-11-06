import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { SessionService } from './core/services/session.service';
import { NAVEGACAO_DE_AUTENTICACAO } from './core/services/navegacao-de-autenticacao';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected readonly session = inject(SessionService);
  private readonly navegacao = inject(NAVEGACAO_DE_AUTENTICACAO);

  sair(): void {
    this.session.logout().subscribe((redirectUrl) => this.navegacao.irPara(redirectUrl));
  }
}
