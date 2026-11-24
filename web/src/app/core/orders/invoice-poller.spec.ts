import { HttpErrorResponse, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { invoice } from '../../../../testing/fixtures';
import { INVOICE_MAX_ATTEMPTS, INVOICE_TIMEOUT, InvoicePollState, pollInvoice, retryAfterSeconds } from './invoice-poller';

function accepted(retryAfter?: string): HttpResponse<unknown> {
  const headers = retryAfter === undefined ? new HttpHeaders() : new HttpHeaders({ 'Retry-After': retryAfter });
  return new HttpResponse({ status: 202, body: { status: 'Pending' }, headers });
}

const ready = new HttpResponse<unknown>({ status: 200, body: invoice });

describe('pollInvoice', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function start(responses: (() => Observable<HttpResponse<unknown>>)[]) {
    const fetch = vi.fn(() => (responses.shift() ?? (() => of(accepted('1'))))());
    const states: InvoicePollState[] = [];
    const subscription = pollInvoice(fetch).subscribe((state) => states.push(state));
    return { fetch, states, subscription };
  }

  it('waits Retry-After seconds between 202s and finishes with the invoice (202 -> 202 -> 200)', async () => {
    const { fetch, states } = start([() => of(accepted('3')), () => of(accepted('1')), () => of(ready)]);
    expect(fetch).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(2999);
    expect(fetch).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(fetch).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(1000);

    expect(fetch).toHaveBeenCalledTimes(3);
    expect(states).toEqual([
      { state: 'pending', attempt: 1 },
      { state: 'pending', attempt: 2 },
      { state: 'pending', attempt: 3 },
      { state: 'ready', invoice },
    ]);
  });

  it(`stops after ${INVOICE_MAX_ATTEMPTS} attempts`, async () => {
    const { fetch, states } = start([]);

    await vi.advanceTimersByTimeAsync(60_000);

    expect(fetch).toHaveBeenCalledTimes(INVOICE_MAX_ATTEMPTS);
    expect(states.at(-1)).toEqual({ state: 'error', error: INVOICE_TIMEOUT });
  });

  it('reports an API error without retrying', async () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { title: 'Invoice unavailable' } });
    const { fetch, states } = start([() => throwError(() => conflict)]);

    await vi.advanceTimersByTimeAsync(10_000);

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(states.at(-1)).toEqual({ state: 'error', error: conflict });
  });

  it('stops polling when unsubscribed', async () => {
    const { fetch, subscription } = start([]);
    await vi.advanceTimersByTimeAsync(1000);
    expect(fetch).toHaveBeenCalledTimes(2);

    subscription.unsubscribe();
    await vi.advanceTimersByTimeAsync(30_000);

    expect(fetch).toHaveBeenCalledTimes(2);
  });
});

describe('retryAfterSeconds', () => {
  it('reads the header, defaulting to 2 and capping at 5', () => {
    expect(retryAfterSeconds('0')).toBe(0);
    expect(retryAfterSeconds('3')).toBe(3);
    expect(retryAfterSeconds('30')).toBe(5);
    expect(retryAfterSeconds(null)).toBe(2);
    expect(retryAfterSeconds('')).toBe(2);
    expect(retryAfterSeconds('soon')).toBe(2);
    expect(retryAfterSeconds('-1')).toBe(2);
  });
});
