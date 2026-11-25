import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatButtonToggleHarness } from '@angular/material/button-toggle/testing';
import { page } from 'vitest/browser';
import { price, products } from '../../../../../testing/fixtures';
import { ProductCardComponent } from './product-card.component';

describe('ProductCardComponent', () => {
  let http: HttpTestingController;

  async function render(asOf?: string, canBuy?: boolean) {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ProductCardComponent);
    fixture.componentRef.setInput('product', products[0]);
    if (asOf) fixture.componentRef.setInput('asOf', asOf);
    if (canBuy !== undefined) fixture.componentRef.setInput('canBuy', canBuy);
    const added = vi.fn();
    fixture.componentInstance.added.subscribe(added);
    await fixture.whenStable();
    return { fixture, added, loader: TestbedHarnessEnvironment.loader(fixture) };
  }

  const priceRequest = (skuId: number) => http.expectOne((r) => r.url.endsWith(`/skus/${skuId}/price`));

  afterEach(() => http.verify());

  it('asks anonymous visitors to sign in instead of offering Add to cart', async () => {
    await render(undefined, false);
    priceRequest(11).flush(price(11, 300));

    const signIn = page.getByRole('link', { name: 'Sign in to buy' });
    await expect.element(signIn).toHaveAttribute('href', '/login?returnUrl=%2F');
    await expect.element(page.getByRole('button', { name: 'Add to cart' })).not.toBeInTheDocument();
    await expect.element(page.getByTestId('price')).toMatchTextContent(/300\.00/);
  });

  it('shows the product, a loading placeholder, then the price of the first SKU', async () => {
    const { fixture } = await render();

    await expect.element(page.getByRole('heading', { name: 'Ethiopia Yirgacheffe' })).toBeVisible();
    await expect.element(page.getByTestId('product-origin')).toHaveTextContent('Ethiopia');
    await expect.element(page.getByTestId('roast-chip')).toMatchTextContent(/Light roast/);
    await expect.element(page.getByTestId('price-loading')).toBeVisible();
    await expect.element(page.getByRole('button', { name: 'Add to cart' })).toBeDisabled();

    priceRequest(11).flush(price(11, 300));
    await fixture.whenStable();

    await expect.element(page.getByTestId('price')).toMatchTextContent(/300\.00/);
    await expect.element(page.getByRole('button', { name: 'Add to cart' })).toBeEnabled();
  });

  it('asks for prices as of the given instant', async () => {
    await render('2026-12-01T00:00:00Z');

    expect(priceRequest(11).request.params.get('asOf')).toBe('2026-12-01T00:00:00Z');
  });

  it('refreshes the price when the SKU changes and drops the stale request', async () => {
    const { fixture, loader } = await render();
    const first = priceRequest(11);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: '1000 g' }))).check();
    await fixture.whenStable();

    expect(first.cancelled).toBe(true);
    priceRequest(12).flush(price(12, 560));
    await fixture.whenStable();

    await expect.element(page.getByTestId('price')).toMatchTextContent(/560\.00/);
  });

  it('keeps the button disabled when the SKU has no price', async () => {
    const { fixture } = await render();

    priceRequest(11).flush({ title: 'No effective price' }, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    await expect.element(page.getByTestId('price-unavailable')).toMatchTextContent(/Price unavailable/);
    await expect.element(page.getByRole('button', { name: 'Add to cart' })).toBeDisabled();
  });

  it('shows no SKU hint for a product without SKUs', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ProductCardComponent);
    fixture.componentRef.setInput('product', { ...products[0], skus: [] });
    await fixture.whenStable();

    await expect.element(page.getByTestId('sku-picker-empty')).toBeVisible();
    await expect.element(page.getByTestId('price-none')).not.toBeInTheDocument();
    await expect.element(page.getByRole('button', { name: 'Add to cart' })).toBeDisabled();
  });

  it('emits the chosen SKU with its price', async () => {
    const { fixture, added } = await render();
    priceRequest(11).flush(price(11, 300));
    await fixture.whenStable();

    await page.getByRole('button', { name: 'Add to cart' }).click();

    expect(added).toHaveBeenCalledExactlyOnceWith({
      skuId: 11,
      productId: 1,
      name: 'Ethiopia Yirgacheffe',
      roast: 'Light',
      grind: 'WholeBean',
      sizeG: 500,
      unitPrice: 300,
      currency: 'MXN',
    });
  });
});
