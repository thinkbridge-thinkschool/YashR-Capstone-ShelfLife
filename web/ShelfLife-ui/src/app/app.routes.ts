import { Routes } from '@angular/router';
import { authGuard } from './shared/guards/auth.guard';
import { librarianGuard } from './shared/guards/librarian.guard';
import { memberGuard } from './shared/guards/member.guard';

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

      // ── Librarian-only routes ────────────────────────────────────────────
      {
        path: 'catalog/add-book',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./catalog/add-book/add-book.component').then(m => m.AddBookComponent),
      },
      {
        path: 'catalog/add-copy',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./catalog/add-copy/add-copy.component').then(m => m.AddCopyComponent),
      },
      {
        path: 'lending/loans',
        canActivate: [librarianGuard],
        data: { activeOnly: true, title: 'Active Loans' },
        loadComponent: () =>
          import('./lending/loans/loans.component').then(m => m.LoansComponent),
      },
      {
        path: 'lending/loan-history',
        canActivate: [librarianGuard],
        data: { activeOnly: false, title: 'Loan History' },
        loadComponent: () =>
          import('./lending/loans/loans.component').then(m => m.LoansComponent),
      },
      {
        path: 'lending/borrow',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./lending/borrow-book/borrow-book.component').then(m => m.BorrowBookComponent),
      },
      {
        path: 'lending/return',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./lending/return-book/return-book.component').then(m => m.ReturnBookComponent),
      },
      {
        path: 'insights/popular',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./insights/popular-books/popular-books.component').then(m => m.PopularBooksComponent),
      },
      {
        path: 'insights/overdue',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./insights/overdue-loans/overdue-loans.component').then(m => m.OverdueLoansComponent),
      },
      {
        path: 'identity/members',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./identity/members/members.component').then(m => m.MembersComponent),
      },
      {
        path: 'lending/holds',
        canActivate: [librarianGuard],
        loadComponent: () =>
          import('./lending/holds/holds.component').then(m => m.HoldsComponent),
      },

      // ── Member-only routes ───────────────────────────────────────────────
      {
        path: 'catalog/search',
        canActivate: [memberGuard],
        loadComponent: () =>
          import('./catalog/search-books/search-books.component').then(m => m.SearchBooksComponent),
      },
      {
        path: 'lending/my-loans',
        canActivate: [memberGuard],
        loadComponent: () =>
          import('./lending/my-loans/my-loans.component').then(m => m.MyLoansComponent),
      },
      {
        path: 'lending/my-holds',
        canActivate: [memberGuard],
        loadComponent: () =>
          import('./lending/my-holds/my-holds.component').then(m => m.MyHoldsComponent),
      },

      // ── Shared routes (any authenticated user) ───────────────────────────
      {
        path: 'identity/profile',
        loadComponent: () =>
          import('./identity/profile/profile.component').then(m => m.ProfileComponent),
      },

      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
