import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    title: 'Shop · Roastery',
    loadComponent: () => import('./pages/home/home-page.component').then((m) => m.HomePageComponent),
  },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    title: 'Admin · Roastery',
    loadComponent: () => import('./pages/admin/admin-page.component').then((m) => m.AdminPageComponent),
  },
  {
    path: 'login',
    title: 'Sign in · Roastery',
    loadComponent: () => import('./pages/login/login-page.component').then((m) => m.LoginPageComponent),
  },
  {
    path: 'register',
    title: 'Create account · Roastery',
    loadComponent: () => import('./pages/register/register-page.component').then((m) => m.RegisterPageComponent),
  },
  { path: '**', redirectTo: '' },
];
