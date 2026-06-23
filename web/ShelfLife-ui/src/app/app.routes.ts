import { Routes } from '@angular/router';
import { authGuard } from './shared/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./identity/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./identity/register/register.component').then(m => m.RegisterComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/shell/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/dashboard.component').then(m => m.DashboardComponent),
      },
      {
        path: 'catalog/add-book',
        loadComponent: () =>
          import('./catalog/add-book/add-book.component').then(m => m.AddBookComponent),
      },
      {
        path: 'catalog/add-copy',
        loadComponent: () =>
          import('./catalog/add-copy/add-copy.component').then(m => m.AddCopyComponent),
      },
      {
        path: 'lending/borrow',
        loadComponent: () =>
          import('./lending/borrow-book/borrow-book.component').then(m => m.BorrowBookComponent),
      },
      {
        path: 'lending/return',
        loadComponent: () =>
          import('./lending/return-book/return-book.component').then(m => m.ReturnBookComponent),
      },
      {
        path: 'insights/popular',
        loadComponent: () =>
          import('./insights/popular-books/popular-books.component').then(m => m.PopularBooksComponent),
      },
      {
        path: 'insights/overdue',
        loadComponent: () =>
          import('./insights/overdue-loans/overdue-loans.component').then(m => m.OverdueLoansComponent),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
