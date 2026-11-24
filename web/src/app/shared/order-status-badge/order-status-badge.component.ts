import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

const STATUS_ICONS: Record<string, string> = {
  Pending: 'schedule',
  Paid: 'paid',
  Preparing: 'coffee_maker',
  Shipped: 'local_shipping',
  Delivered: 'check_circle',
  Cancelled: 'cancel',
};

/** Order status as icon plus text, so color is never the only signal. */
@Component({
  selector: 'app-order-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatChipsModule, MatIconModule],
  template: `
    <mat-chip-set aria-label="Order status">
      <mat-chip disableRipple data-testid="order-status" [attr.data-status]="status()" [highlighted]="isClosed()">
        <mat-icon matChipAvatar aria-hidden="true" data-testid="order-status-icon">{{ icon() }}</mat-icon>
        <span data-testid="order-status-label">{{ status() }}</span>
      </mat-chip>
    </mat-chip-set>
  `,
})
export class OrderStatusBadgeComponent {
  readonly status = input.required<string>();

  protected readonly icon = computed(() => STATUS_ICONS[this.status()] ?? 'help');
  protected readonly isClosed = computed(() => this.status() === 'Delivered' || this.status() === 'Cancelled');
}
