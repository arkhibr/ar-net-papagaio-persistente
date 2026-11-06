import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { CategoriasComponent } from './features/catalogo/categorias/categorias.component';
import { ChamadoDetalheComponent } from './features/chamados/chamado-detalhe/chamado-detalhe.component';
import { AbrirChamadoComponent } from './features/chamados/abrir-chamado/abrir-chamado.component';
import { MeusChamadosComponent } from './features/chamados/meus-chamados/meus-chamados.component';
import { FilaComponent } from './features/equipe/fila/fila.component';
import { AdministracaoCategoriasComponent } from './features/administracao/categorias/administracao-categorias.component';
import { AdministracaoEquipesComponent } from './features/administracao/equipes/administracao-equipes.component';

export const routes: Routes = [
  { path: 'categorias', component: CategoriasComponent, canActivate: [authGuard] },
  { path: 'chamados/novo', component: AbrirChamadoComponent, canActivate: [authGuard] },
  { path: 'chamados/meus', component: MeusChamadosComponent, canActivate: [authGuard] },
  { path: 'chamados/:id', component: ChamadoDetalheComponent, canActivate: [authGuard] },
  {
    path: 'equipe/fila',
    component: FilaComponent,
    canActivate: [authGuard, roleGuard(['Tecnico', 'Supervisor'])],
  },
  {
    path: 'administracao/categorias',
    component: AdministracaoCategoriasComponent,
    canActivate: [authGuard, roleGuard(['Supervisor'])],
  },
  {
    path: 'administracao/equipes',
    component: AdministracaoEquipesComponent,
    canActivate: [authGuard, roleGuard(['Supervisor'])],
  },
  { path: '', redirectTo: 'chamados/meus', pathMatch: 'full' },
  { path: '**', redirectTo: 'chamados/meus' },
];
