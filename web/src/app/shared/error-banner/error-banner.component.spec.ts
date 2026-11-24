import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { ErrorBannerComponent } from './error-banner.component';

describe('ErrorBannerComponent', () => {
  async function render(error: unknown) {
    const fixture = TestBed.createComponent(ErrorBannerComponent);
    fixture.componentRef.setInput('error', error);
    await fixture.whenStable();
  }

  const httpError = (status: number, body: unknown) => new HttpErrorResponse({ status, error: body });

  it('renders nothing without an error', async () => {
    await render(null);

    await expect.element(page.getByRole('alert')).not.toBeInTheDocument();
  });

  it('shows the title and detail of a 409 ProblemDetails', async () => {
    await render(httpError(409, { title: 'Duplicate price', detail: 'SKU 1 already has a price.', status: 409 }));

    await expect.element(page.getByRole('alert')).toBeVisible();
    await expect.element(page.getByTestId('error-title')).toHaveTextContent('Duplicate price');
    await expect.element(page.getByTestId('error-detail')).toHaveTextContent('SKU 1 already has a price.');
  });

  it('lists every field error of a validation problem', async () => {
    await render(
      httpError(400, {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { password: ['Passwords must have at least one digit.', 'Passwords must have at least one uppercase.'] },
      }),
    );

    await expect.element(page.getByRole('listitem')).toHaveLength(2);
    await expect.element(page.getByRole('listitem').first()).toHaveTextContent('Passwords must have at least one digit.');
  });

  it('shows a 422 domain problem', async () => {
    await render(httpError(422, { title: 'Effective date in the past', detail: 'effectiveFrom must be today (UTC) or later.' }));

    await expect.element(page.getByTestId('error-title')).toHaveTextContent('Effective date in the past');
  });

  it('explains when the server cannot be reached', async () => {
    await render(httpError(0, null));

    await expect.element(page.getByRole('alert')).toBeVisible();
    await expect.element(page.getByTestId('error-title')).toHaveTextContent('Cannot reach the server');
  });

  it('keeps a long message inside the banner', async () => {
    const long = 'x'.repeat(400);
    await render(httpError(422, { title: 'Problem', detail: long }));

    const banner = page.getByTestId('error-banner').element();
    expect(banner.scrollWidth).toBeLessThanOrEqual(banner.clientWidth);
  });
});
