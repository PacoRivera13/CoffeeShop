import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Original coffee-bag placeholder, tinted by roast level through theme tokens. */
@Component({
  selector: 'app-product-image',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      viewBox="0 0 120 140"
      role="img"
      data-testid="product-image"
      [attr.data-roast]="roastKey()"
      [attr.aria-label]="alt()"
    >
      <path class="bag" d="M24 20 L96 20 L104 132 Q104 136 100 136 L20 136 Q16 136 16 132 Z" />
      <path class="fold" d="M24 20 L32 6 L88 6 L96 20 Z" />
      <rect class="label" x="34" y="56" width="52" height="44" rx="6" />
      <ellipse class="bean" cx="60" cy="78" rx="12" ry="16" />
      <path class="seam" d="M60 64 Q54 78 60 92" />
    </svg>
  `,
  styles: `
    :host {
      display: block;
    }
    svg {
      width: 100%;
      height: auto;
      display: block;
    }
    .bag {
      fill: var(--roast-color);
    }
    .fold {
      fill: var(--roast-color);
      opacity: 0.85;
    }
    .label {
      fill: var(--mat-sys-surface-container-lowest);
    }
    .bean {
      fill: var(--roast-color);
    }
    .seam {
      fill: none;
      stroke: var(--mat-sys-surface-container-lowest);
      stroke-width: 2.5;
      stroke-linecap: round;
    }
    [data-roast='light'] {
      --roast-color: var(--mat-sys-tertiary-fixed-dim);
    }
    [data-roast='medium'] {
      --roast-color: var(--mat-sys-tertiary);
    }
    [data-roast='dark'],
    [data-roast='other'] {
      --roast-color: var(--mat-sys-primary);
    }
  `,
})
export class ProductImageComponent {
  readonly name = input.required<string>();
  readonly roast = input.required<string>();

  protected readonly roastKey = computed(() => {
    const roast = this.roast().toLowerCase();
    return roast === 'light' || roast === 'medium' || roast === 'dark' ? roast : 'other';
  });
  protected readonly alt = computed(() => `${this.name()}, ${this.roast().toLowerCase()} roast coffee bag`);
}
