import { ChangeDetectionStrategy, Component, computed, inject, input, LOCALE_ID, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { InventoryResponse } from '../../../api/models/inventory-response';
import { formatGrams, formatKg, isLowStock } from '../admin-rules';

/** One product's stock in grams and kilograms, flagged when low. */
@Component({
  selector: 'app-stock-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatChipsModule, MatIconModule],
  template: `
    <div class="row" data-testid="stock-row" [class.selected]="selected()">
      <div class="info">
        <h3 class="name" data-testid="stock-name">{{ item().name }}</h3>
        <p class="amounts">
          <span class="tabular-nums" data-testid="stock-grams">{{ grams() }}</span>
          <span class="muted tabular-nums" data-testid="stock-kg">{{ kg() }}</span>
        </p>
      </div>
      @if (low()) {
        <mat-chip-set aria-label="Stock status">
          <mat-chip disableRipple data-testid="low-stock-chip">
            <mat-icon matChipAvatar aria-hidden="true">warning</mat-icon>
            Low stock
          </mat-chip>
        </mat-chip-set>
      }
      <button
        mat-stroked-button
        type="button"
        data-testid="select-product"
        [attr.aria-pressed]="selected()"
        [attr.aria-label]="'Manage ' + item().name"
        (click)="choose.emit()"
      >
        <mat-icon aria-hidden="true">{{ selected() ? 'check' : 'history' }}</mat-icon>
        Manage
      </button>
    </div>
  `,
  styles: `
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 16px;
      padding: 12px 16px;
      border-radius: 12px;
    }
    .row.selected {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .info {
      flex: 1 1 180px;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .name {
      margin: 0;
      font: var(--mat-sys-title-medium);
      color: inherit;
    }
    .amounts {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 12px;
      margin: 2px 0 0;
    }
    .muted {
      font: var(--mat-sys-body-medium);
    }
    button {
      min-height: 44px;
    }
  `,
})
export class StockRowComponent {
  readonly item = input.required<InventoryResponse>();
  readonly selected = input(false);
  readonly choose = output<void>();

  private readonly locale = inject(LOCALE_ID);
  protected readonly grams = computed(() => formatGrams(Number(this.item().stockG), this.locale));
  protected readonly kg = computed(() => formatKg(Number(this.item().stockG), this.locale));
  protected readonly low = computed(() => isLowStock(Number(this.item().stockG)));
}
