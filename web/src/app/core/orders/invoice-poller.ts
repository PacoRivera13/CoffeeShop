import { HttpResponse } from '@angular/common/http';
import { catchError, concat, Observable, of, switchMap, timer } from 'rxjs';
import { InvoiceResponse } from '../../api/models/invoice-response';

export const INVOICE_MAX_ATTEMPTS = 10;
export const DEFAULT_RETRY_AFTER_SECONDS = 2;
export const MAX_RETRY_AFTER_SECONDS = 5;

export type InvoicePollState =
  | { state: 'pending'; attempt: number }
  | { state: 'ready'; invoice: InvoiceResponse }
  | { state: 'error'; error: unknown };

/** Shown when the invoice is still not ready after the last attempt. */
export const INVOICE_TIMEOUT = {
  status: 202,
  title: 'Invoice not ready yet',
  detail: 'The invoice is still being prepared. Try again in a minute.',
} as const;

/**
 * Asks for the invoice until it is ready: a 202 is retried after its Retry-After seconds (default 2, capped at 5),
 * at most INVOICE_MAX_ATTEMPTS requests in all. Unsubscribing stops the timer and any request in flight.
 */
export function pollInvoice(fetch: () => Observable<HttpResponse<unknown>>): Observable<InvoicePollState> {
  const attempt = (n: number): Observable<InvoicePollState> =>
    concat(
      of<InvoicePollState>({ state: 'pending', attempt: n }),
      fetch().pipe(
        switchMap((response): Observable<InvoicePollState> => {
          if (response.status !== 202) return of({ state: 'ready', invoice: response.body as InvoiceResponse });
          if (n >= INVOICE_MAX_ATTEMPTS) return of({ state: 'error', error: INVOICE_TIMEOUT });
          return timer(retryAfterSeconds(response.headers.get('Retry-After')) * 1000).pipe(
            switchMap(() => attempt(n + 1)),
          );
        }),
        catchError((error: unknown) => of<InvoicePollState>({ state: 'error', error })),
      ),
    );
  return attempt(1);
}

export function retryAfterSeconds(header: string | null): number {
  const seconds = header === null || header.trim() === '' ? Number.NaN : Number(header);
  if (!Number.isFinite(seconds) || seconds < 0) return DEFAULT_RETRY_AFTER_SECONDS;
  return Math.min(seconds, MAX_RETRY_AFTER_SECONDS);
}
