import { ChangeDetectionStrategy, Component, computed, input, LOCALE_ID, inject } from '@angular/core';

@Component({
  selector: 'app-price-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="price tabular-nums" data-testid="price">{{ formatted() }}</span>`,
  styles: `
    .price {
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
      white-space: nowrap;
    }
  `,
})
export class PriceBadgeComponent {
  readonly amount = input.required<number>();
  readonly currency = input.required<string>();
  readonly locale = input<string>(inject(LOCALE_ID));

  protected readonly formatted = computed(() => {
    try {
      return new Intl.NumberFormat(this.locale(), { style: 'currency', currency: this.currency() }).format(this.amount());
    } catch {
      // An unknown currency code still shows the amount instead of breaking the page.
      return `${this.amount().toFixed(2)} ${this.currency()}`;
    }
  });
}
