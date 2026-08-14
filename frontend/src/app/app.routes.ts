import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tableau-de-bord' },
  {
    path: 'tableau-de-bord',
    loadComponent: () =>
      import('./pages/dashboard/dashboard.component').then((m) => m.DashboardComponent),
  },
  {
    path: 'corrections',
    loadComponent: () =>
      import('./pages/corrections/corrections.component').then((m) => m.CorrectionsComponent),
  },
  {
    path: 'historique',
    loadComponent: () =>
      import('./pages/history/history.component').then((m) => m.HistoryComponent),
  },
  { path: '**', redirectTo: 'tableau-de-bord' },
];
