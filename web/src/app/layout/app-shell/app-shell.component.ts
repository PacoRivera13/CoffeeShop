import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { breakpointQueries } from '../../core/layout/breakpoints';
import { ThemeToggleComponent } from '../../shared/theme-toggle/theme-toggle.component';

export interface NavLink {
  label: string;
  path: string;
}

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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

  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe(breakpointQueries().compact)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly links = computed<NavLink[]>(() => {
    if (this.auth.isAdmin()) return [{ label: 'Shop', path: '/' }, { label: 'Admin', path: '/admin' }];
    if (this.auth.isAuthenticated()) return [{ label: 'Shop', path: '/' }];
    return [
      { label: 'Sign in', path: '/login' },
      { label: 'Create account', path: '/register' },
    ];
  });

  protected readonly cartLabel = computed(() => `Cart, ${this.cartCount()} ${this.cartCount() === 1 ? 'item' : 'items'}`);

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
