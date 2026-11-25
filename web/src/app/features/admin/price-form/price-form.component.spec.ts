import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { PriceFormComponent } from './price-form.component';

describe('PriceFormComponent', () => {
  async function render() {
    const fixture = TestBed.createComponent(PriceFormComponent);
    const save = vi.fn();
    fixture.componentInstance.save.subscribe(save);
    await fixture.whenStable();
    return { fixture, save };
  }

  const submit = () => page.getByRole('button', { name: 'Add price' }).click();

  afterEach(() => vi.useRealTimers());

  it('requires an amount and a date, with MXN as the default currency', async () => {
    const { save } = await render();

    await expect.element(page.getByLabelText('Currency')).toHaveValue('MXN');
    await submit();

    await expect.element(page.getByText('Amount is required.')).toBeVisible();
    await expect.element(page.getByText('Effective date is required.')).toBeVisible();
    expect(save).not.toHaveBeenCalled();
  });

  it('rejects a zero amount and a currency that is not 3 letters', async () => {
    const { save } = await render();

    await page.getByLabelText('Amount').fill('0');
    await page.getByLabelText('Currency').fill('MX');
    await submit();

    await expect.element(page.getByText('Amount must be greater than 0.')).toBeVisible();
    await expect.element(page.getByText('Use a 3-letter currency code, e.g. MXN.')).toBeVisible();
    expect(save).not.toHaveBeenCalled();
  });

  it('sets the minimum date to today in UTC, even late in the evening west of UTC', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-25T23:30:00-06:00'));
    await render();

    await expect.element(page.getByTestId('effective-from')).toHaveAttribute('min', '2026-11-26');
    await page.getByTestId('effective-from').fill('2026-11-25');
    await submit();

    await expect.element(page.getByText('Choose today or a later date.')).toBeVisible();
  });

  it('emits the price with an upper-case currency', async () => {
    const { save } = await render();

    await page.getByLabelText('Amount').fill('310.5');
    await page.getByLabelText('Currency').fill('mxn');
    await page.getByTestId('effective-from').fill('2099-12-01');
    await submit();

    expect(save).toHaveBeenCalledExactlyOnceWith({ amount: 310.5, currency: 'MXN', effectiveFrom: '2099-12-01' });
  });

  it('shows a duplicate date reported by the server on the date field', async () => {
    const { fixture } = await render();
    await page.getByTestId('effective-from').fill('2099-12-01');

    fixture.componentRef.setInput('conflict', 'duplicate');
    await fixture.whenStable();

    await expect.element(page.getByText('A price already starts on this date.')).toBeVisible();
  });

  it('shows no required errors after a reset that follows a submit', async () => {
    const { fixture } = await render();
    await page.getByLabelText('Amount').fill('310');
    await page.getByTestId('effective-from').fill('2099-12-01');
    await submit();

    fixture.componentInstance.reset();
    await fixture.whenStable();

    await expect.element(page.getByText('Amount is required.')).not.toBeInTheDocument();
    await expect.element(page.getByText('Effective date is required.')).not.toBeInTheDocument();
  });

  it('drops a server date error once the conflict is cleared', async () => {
    const { fixture, save } = await render();
    await page.getByLabelText('Amount').fill('310');
    await page.getByTestId('effective-from').fill('2099-12-01');
    fixture.componentRef.setInput('conflict', 'duplicate');
    await fixture.whenStable();
    await expect.element(page.getByText('A price already starts on this date.')).toBeVisible();

    fixture.componentRef.setInput('conflict', null);
    await fixture.whenStable();
    await submit();

    await expect.element(page.getByText('A price already starts on this date.')).not.toBeInTheDocument();
    expect(save).toHaveBeenCalledOnce();
  });

  it('disables saving while pending and resets amount and date afterwards', async () => {
    const { fixture } = await render();
    await page.getByLabelText('Amount').fill('310');
    fixture.componentRef.setInput('pending', true);
    await fixture.whenStable();
    await expect.element(page.getByTestId('save-price')).toBeDisabled();

    fixture.componentInstance.reset();
    fixture.componentRef.setInput('pending', false);
    await fixture.whenStable();

    await expect.element(page.getByLabelText('Amount')).toHaveValue(null);
    await expect.element(page.getByLabelText('Currency')).toHaveValue('MXN');
  });
});
