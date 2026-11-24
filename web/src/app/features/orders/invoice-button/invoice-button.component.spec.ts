import { HttpErrorResponse, HttpHeaders, HttpResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { invoice } from '../../../../../testing/fixtures';
import { ApiRx } from '../../../core/http/api-rx.service';
import { INVOICE_MAX_ATTEMPTS } from '../../../core/orders/invoice-poller';
import { InvoiceButtonComponent } from './invoice-button.component';

const accepted = () =>
  of(new HttpResponse({ status: 202, body: { status: 'Pending' }, headers: new HttpHeaders({ 'Retry-After': '1' }) }));
const ready = () => of(new HttpResponse({ status: 200, body: invoice }));

// Fake timers freeze the polling of expect.element, so these tests read the DOM synchronously (by test id) after
// rendering explicitly; the timers being faked are exactly what the polling logic waits on.
describe('InvoiceButtonComponent', () => {
  let responses: (() => Observable<HttpResponse<unknown>>)[];
  let fetch: ReturnType<typeof vi.fn>;
  let open: ReturnType<typeof vi.spyOn>;

  async function render() {
    fetch = vi.fn((_fn: unknown, params: { id: number }) => {
      expect(params.id).toBe(7);
      return (responses.shift() ?? accepted)();
    });
    open = vi.spyOn(window, 'open').mockReturnValue(null);
    TestBed.configureTestingModule({ providers: [{ provide: ApiRx, useValue: { response: fetch } }] });
    const fixture = TestBed.createComponent(InvoiceButtonComponent);
    fixture.componentRef.setInput('orderId', 7);
    const failed = vi.fn();
    fixture.componentInstance.failed.subscribe(failed);
    await fixture.whenStable();
    vi.useFakeTimers();
    return { fixture, failed };
  }

  function byTestId(fixture: ComponentFixture<unknown>, id: string): HTMLElement | null {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`);
  }

  function clickInvoice(fixture: ComponentFixture<unknown>): void {
    (byTestId(fixture, 'invoice-button') as HTMLButtonElement).click();
  }

  beforeEach(() => (responses = []));
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('polls 202 -> 202 -> 200, showing progress, then opens the invoice in a new tab', async () => {
    responses = [accepted, accepted, ready];
    const { fixture } = await render();

    clickInvoice(fixture);
    expect(byTestId(fixture, 'invoice-spinner')).not.toBeNull();
    expect((byTestId(fixture, 'invoice-button') as HTMLButtonElement).disabled).toBe(true);
    expect(byTestId(fixture, 'invoice-status')!.textContent).toBe('Preparing the invoice…');

    await vi.advanceTimersByTimeAsync(1000);
    expect(byTestId(fixture, 'invoice-status')!.textContent).toMatch(/attempt 2 of 10/);
    expect(open).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1000);

    expect(fetch).toHaveBeenCalledTimes(3);
    expect(open).toHaveBeenCalledExactlyOnceWith(invoice.url, '_blank', 'noopener');
    expect((byTestId(fixture, 'invoice-button') as HTMLButtonElement).disabled).toBe(false);
    expect(byTestId(fixture, 'invoice-spinner')).toBeNull();
    expect(byTestId(fixture, 'invoice-link')!.getAttribute('href')).toBe(invoice.url);
    expect(byTestId(fixture, 'invoice-status')!.getAttribute('aria-live')).toBe('polite');
    expect(byTestId(fixture, 'invoice-status')!.textContent).toMatch(/is ready\. If no new tab opened, use the Open invoice link/);
  });

  it(`gives up after ${INVOICE_MAX_ATTEMPTS} attempts and reports it`, async () => {
    const { fixture, failed } = await render();

    clickInvoice(fixture);
    await vi.advanceTimersByTimeAsync(30_000);

    expect(fetch).toHaveBeenCalledTimes(INVOICE_MAX_ATTEMPTS);
    expect(failed).toHaveBeenCalledOnce();
    expect(open).not.toHaveBeenCalled();
    expect((byTestId(fixture, 'invoice-button') as HTMLButtonElement).disabled).toBe(false);
  });

  it('reports a 409 without retrying', async () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { title: 'Invoice unavailable' } });
    responses = [() => throwError(() => conflict)];
    const { fixture, failed } = await render();

    clickInvoice(fixture);
    await vi.advanceTimersByTimeAsync(5000);

    expect(failed).toHaveBeenCalledExactlyOnceWith(conflict);
    expect(fetch).toHaveBeenCalledOnce();
  });

  it('stops polling when destroyed', async () => {
    const { fixture } = await render();
    clickInvoice(fixture);
    await vi.advanceTimersByTimeAsync(1000);
    expect(fetch).toHaveBeenCalledTimes(2);

    fixture.destroy();
    await vi.advanceTimersByTimeAsync(30_000);

    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
