import { ChangeDetectionStrategy, Component, computed, input, linkedSignal, output } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { SkuResponse } from '../../api/models/sku-response';

const GRIND_LABELS: Record<string, string> = { WholeBean: 'Whole bean', Ground: 'Ground' };

/** Picks a SKU by grind and bag size; emits once both are chosen and that combination exists. */
@Component({
  selector: 'app-sku-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonToggleModule],
  template: `
    @if (skus().length === 0) {
      <p data-testid="sku-picker-empty">No sizes available.</p>
    } @else {
      <div class="group">
        <span class="label" id="grind-label">Grind</span>
        <mat-button-toggle-group
          aria-labelledby="grind-label"
          data-testid="grind-group"
          [value]="grind()"
          (change)="selectGrind($event.value)"
        >
          @for (option of grinds(); track option) {
            <mat-button-toggle [value]="option" [disabled]="!isAvailable(option, size())">
              {{ grindLabel(option) }}
            </mat-button-toggle>
          }
        </mat-button-toggle-group>
      </div>
      <div class="group">
        <span class="label" id="size-label">Size</span>
        <mat-button-toggle-group
          aria-labelledby="size-label"
          data-testid="size-group"
          [value]="size()"
          (change)="selectSize($event.value)"
        >
          @for (option of sizes(); track option) {
            <mat-button-toggle [value]="option" [disabled]="!isAvailable(grind(), option)">
              {{ option }} g
            </mat-button-toggle>
          }
        </mat-button-toggle-group>
      </div>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 12px;
    }
    .group {
      display: grid;
      gap: 4px;
    }
    .label {
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    mat-button-toggle-group {
      flex-wrap: wrap;
      max-width: 100%;
    }
  `,
})
export class SkuPickerComponent {
  readonly skus = input.required<readonly SkuResponse[]>();
  /** The SKU shown as chosen, if any; the user's later choices replace it. */
  readonly selectedId = input<number | null>(null);
  readonly skuChange = output<SkuResponse>();

  private readonly selected = computed(() => this.skus().find((s) => Number(s.id) === this.selectedId()) ?? null);
  protected readonly grind = linkedSignal<string | null>(() => this.selected()?.grind ?? null);
  protected readonly size = linkedSignal<number | null>(() => {
    const sku = this.selected();
    return sku ? Number(sku.sizeG) : null;
  });

  protected readonly grinds = computed(() => [...new Set(this.skus().map((s) => s.grind))]);
  // The OpenAPI document types numbers as number | string (the API also reads numeric strings), so normalize.
  protected readonly sizes = computed(() => [...new Set(this.skus().map((s) => Number(s.sizeG)))].sort((a, b) => a - b));

  protected grindLabel(grind: string): string {
    return GRIND_LABELS[grind] ?? grind;
  }

  protected isAvailable(grind: string | null, size: number | null): boolean {
    return this.skus().some((s) => (grind === null || s.grind === grind) && (size === null || Number(s.sizeG) === size));
  }

  protected selectGrind(grind: string): void {
    this.grind.set(grind);
    // Keep the pair valid: a size this grind does not come in switches to its first size.
    if (this.size() !== null && !this.isAvailable(grind, this.size())) {
      this.size.set(this.sizes().find((size) => this.isAvailable(grind, size)) ?? null);
    }
    this.emitIfComplete();
  }

  protected selectSize(size: number): void {
    this.size.set(size);
    if (this.grind() !== null && !this.isAvailable(this.grind(), size)) {
      this.grind.set(this.grinds().find((grind) => this.isAvailable(grind, size)) ?? null);
    }
    this.emitIfComplete();
  }

  private emitIfComplete(): void {
    const sku = this.skus().find((s) => s.grind === this.grind() && Number(s.sizeG) === this.size());
    if (sku) this.skuChange.emit(sku);
  }
}
