import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { PriceHistoryResponse } from '../../../api/models/price-history-response';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { sortByEffectiveFromDesc } from '../admin-rules';

/** Compact screens: the same price history as stacked cards. */
@Component({
  selector: 'app-price-history-cards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatChipsModule, MatIconModule, PriceBadgeComponent],
  template: `
    <ul class="list" aria-label="Price history" data-testid="price-history-cards">
      @for (price of rows(); track price.id) {
        <li>
          <mat-card appearance="outlined" data-testid="price-card">
            <mat-card-content class="content">
              <div class="row">
                <app-price-badge [amount]="+price.amount" [currency]="price.currency" />
                @if (price.isCurrent) {
                  <mat-chip-set aria-label="Price status">
                    <mat-chip disableRipple highlighted data-testid="current-chip">
                      <mat-icon matChipAvatar aria-hidden="true">check</mat-icon>
                      Current
                    </mat-chip>
                  </mat-chip-set>
                }
              </div>
              <p class="meta" data-testid="price-effective-from">
                From {{ price.effectiveFrom | date: 'medium' : 'UTC' }} UTC
              </p>
            </mat-card-content>
          </mat-card>
        </li>
      }
    </ul>
  `,
  styles: `
    .list {
      display: grid;
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .content {
      display: grid;
      gap: 4px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .meta {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class PriceHistoryCardsComponent {
  readonly history = input.required<readonly PriceHistoryResponse[]>();

  protected readonly rows = computed(() => sortByEffectiveFromDesc(this.history()));
}
