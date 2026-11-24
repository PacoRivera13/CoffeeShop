import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <section class="empty" data-testid="empty-state">
      <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <h2 data-testid="empty-state-title">{{ title() }}</h2>
      @if (message()) {
        <p data-testid="empty-state-message">{{ message() }}</p>
      }
      <div class="actions"><ng-content /></div>
    </section>
  `,
  styles: `
    .empty {
      display: grid;
      justify-items: center;
      gap: 8px;
      padding: 32px 16px;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
      overflow-wrap: anywhere;
    }
    .icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--mat-sys-primary);
    }
    h2 {
      margin: 0;
      font: var(--mat-sys-title-large);
      color: var(--mat-sys-on-surface);
    }
    p {
      margin: 0;
      max-width: 48ch;
    }
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly message = input<string>();
  readonly icon = input('inventory_2');
}
