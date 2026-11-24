import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subscription } from 'rxjs';
import { getOrderInvoice } from '../../../api/fn/orders/get-order-invoice';
import { ApiRx } from '../../../core/http/api-rx.service';
import { INVOICE_MAX_ATTEMPTS, InvoicePollState, pollInvoice } from '../../../core/orders/invoice-poller';

/**
 * Downloads the order's invoice, waiting while it is generated. Leaving the page (destroying the button) stops
 * the polling.
 */
@Component({
  selector: 'app-invoice-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <button
      mat-stroked-button
      type="button"
      data-testid="invoice-button"
      [disabled]="pending()"
      [attr.aria-busy]="pending()"
      (click)="download()"
    >
      @if (pending()) {
        <mat-spinner diameter="20" aria-hidden="true" data-testid="invoice-spinner" />
      } @else {
        <mat-icon aria-hidden="true">receipt_long</mat-icon>
      }
      Invoice
    </button>
    @if (readyInvoice(); as invoice) {
      <!-- A popup opened after polling may be blocked, so the link stays available (it expires in 5 minutes). -->
      <a class="link" data-testid="invoice-link" target="_blank" rel="noopener" [href]="invoice.url">
        Open invoice {{ invoice.invoiceNumber }}
      </a>
    }
    <span class="cdk-visually-hidden" aria-live="polite" data-testid="invoice-status">{{ statusText() }}</span>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 16px;
    }
    .link {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
    }
    button {
      min-height: 44px;
    }
    mat-spinner {
      display: inline-block;
      margin-inline-end: 8px;
    }
  `,
})
export class InvoiceButtonComponent {
  private readonly api = inject(ApiRx);
  private readonly window = inject(DOCUMENT).defaultView;

  readonly orderId = input.required<number>();
  /** The API refused the invoice (409, 404) or it was not ready in time. */
  readonly failed = output<unknown>();

  private readonly state = signal<InvoicePollState | null>(null);
  private subscription: Subscription | null = null;

  protected readonly pending = computed(() => this.state()?.state === 'pending');
  protected readonly readyInvoice = computed(() => {
    const state = this.state();
    return state?.state === 'ready' ? state.invoice : null;
  });
  protected readonly statusText = computed(() => {
    const state = this.state();
    switch (state?.state) {
      case 'pending':
        return state.attempt === 1
          ? 'Preparing the invoice…'
          : `Still preparing the invoice, attempt ${state.attempt} of ${INVOICE_MAX_ATTEMPTS}…`;
      case 'ready':
        // The popup may have been blocked (polling outlives the click), so point to the link as well.
        return `Invoice ${state.invoice.invoiceNumber} is ready. If no new tab opened, use the Open invoice link.`;
      case 'error':
        return 'The invoice could not be downloaded.';
      default:
        return '';
    }
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.subscription?.unsubscribe());
  }

  protected download(): void {
    this.subscription?.unsubscribe();
    const id = this.orderId();
    this.subscription = pollInvoice(() => this.api.response(getOrderInvoice, { id })).subscribe((state) => {
      this.state.set(state);
      if (state.state === 'ready') this.window?.open(state.invoice.url, '_blank', 'noopener');
      if (state.state === 'error') this.failed.emit(state.error);
    });
  }
}
