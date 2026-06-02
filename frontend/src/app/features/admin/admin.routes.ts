import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./admin-overview/admin-overview.component').then(
        (m) => m.AdminOverviewComponent,
      ),
  },
  {
    path: 'users',
    loadComponent: () =>
      import('./admin-users/admin-users.component').then(
        (m) => m.AdminUsersComponent,
      ),
  },
  {
    path: 'users/:id',
    loadComponent: () =>
      import('./admin-user-detail/admin-user-detail.component').then(
        (m) => m.AdminUserDetailComponent,
      ),
  },
];
