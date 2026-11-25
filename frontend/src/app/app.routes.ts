import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'investments' },
  {
    path: 'investments',
    title: 'Investments',
    loadComponent: () => import('./features/investments/investment-list').then((m) => m.InvestmentList),
  },
  {
    path: 'investments/new',
    title: 'Create investment',
    loadComponent: () => import('./features/investments/investment-form').then((m) => m.InvestmentForm),
  },
  {
    path: 'investments/:id',
    title: 'Investment details',
    loadComponent: () => import('./features/investments/investment-detail').then((m) => m.InvestmentDetail),
  },
  {
    path: 'investments/:id/withdraw',
    title: 'Withdraw investment',
    loadComponent: () => import('./features/investments/investment-withdraw').then((m) => m.InvestmentWithdraw),
  },
  { path: '**', redirectTo: 'investments' },
];
