import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { breakpointQueries } from '../../core/layout/breakpoints';
import { ThemeToggleComponent } from '../../shared/theme-toggle/theme-toggle.component';
import { AdminMenuComponent } from '../admin-menu/admin-menu.component';
import { CartSummaryBarComponent } from '../cart-summary-bar/cart-summary-bar.component';

export interface NavLink {
  label: string;
  path: string;
}

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AdminMenuComponent,
    CartSummaryBarComponent,
    MatBadgeModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    ThemeToggleComponent,
  ],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Items in the cart, shown on the toolbar badge. */
  readonly cartCount = input(0);
  /** Estimated cart total and its currency, shown on the compact cart bar. */
  readonly cartTotal = input(0);
  readonly cartCurrency = input<string | null>(null);

  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe(breakpointQueries().compact)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  private readonly drawer = viewChild<MatSidenav>('drawer');

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** Compact screens show the cart as a bottom bar, except on the cart page itself. */
  protected readonly showCartBar = computed(
    () =>
      this.compact() && this.auth.isAuthenticated() && this.cartCount() > 0 && !this.url().startsWith('/cart'),
  );

  constructor() {
    // The drawer only exists for compact screens: close it when the layout widens, or it would cover the page.
    effect(() => {
      if (!this.compact()) void this.drawer()?.close();
    });
  }

  protected readonly links = computed<NavLink[]>(() => {
    const shop = [
      { label: 'Shop', path: '/' },
      { label: 'Orders', path: '/orders' },
    ];
    if (this.auth.isAuthenticated()) return shop;
    return [
      { label: 'Sign in', path: '/login' },
      { label: 'Create account', path: '/register' },
    ];
  });

  /** Admin pages: a menu on expanded screens, a drawer section on compact ones; nothing for customers. */
  protected readonly adminLinks = computed<NavLink[]>(() =>
    this.auth.isAdmin()
      ? [
          { label: 'Prices', path: '/admin/prices' },
          { label: 'Inventory', path: '/admin/inventory' },
        ]
      : [],
  );

  protected readonly cartLabel = computed(() => `Cart, ${this.cartCount()} ${this.cartCount() === 1 ? 'item' : 'items'}`);

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
