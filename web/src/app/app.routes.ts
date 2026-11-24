import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Shop · Roastery',
    loadComponent: () => import('./pages/catalog/catalog-page.component').then((m) => m.CatalogPageComponent),
  },
  {
    path: 'cart',
    canActivate: [authGuard],
    title: 'Cart · Roastery',
    loadComponent: () => import('./pages/cart/cart-page.component').then((m) => m.CartPageComponent),
  },
  {
    path: 'orders',
    canActivate: [authGuard],
    title: 'Orders · Roastery',
    loadComponent: () => import('./pages/orders/orders-page.component').then((m) => m.OrdersPageComponent),
  },
  {
    path: 'orders/:id',
    canActivate: [authGuard],
    title: 'Order · Roastery',
    loadComponent: () =>
      import('./pages/order-detail/order-detail-page.component').then((m) => m.OrderDetailPageComponent),
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
