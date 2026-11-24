import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CartService } from './core/cart/cart.service';
import { AppShellComponent } from './layout/app-shell/app-shell.component';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, AppShellComponent],
  template: `
    <app-shell [cartCount]="cart.count()" [cartTotal]="cart.estimatedTotal()" [cartCurrency]="cart.currency()">
      <router-outlet />
    </app-shell>
  `,
})
export class App {
  protected readonly cart = inject(CartService);
}
