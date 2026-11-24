import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { products } from '../../../../testing/fixtures';
import { CatalogService, describeSku, grindLabel } from './catalog.service';

describe('CatalogService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  it('loads the products once and shares them', async () => {
    const catalog = TestBed.inject(CatalogService);
    const http = TestBed.inject(HttpTestingController);

    const first = firstValueFrom(catalog.products());
    http.expectOne((r) => r.url.endsWith('/products')).flush(products);
    expect(await first).toEqual(products);

    expect(await firstValueFrom(catalog.products())).toEqual(products);
    http.verify();
  });

  it('retries after a failed load', async () => {
    const catalog = TestBed.inject(CatalogService);
    const http = TestBed.inject(HttpTestingController);

    const failed = firstValueFrom(catalog.products()).catch(() => 'failed');
    http.expectOne((r) => r.url.endsWith('/products')).flush(null, { status: 500, statusText: 'Error' });
    expect(await failed).toBe('failed');

    const retried = firstValueFrom(catalog.products());
    http.expectOne((r) => r.url.endsWith('/products')).flush(products);
    expect(await retried).toHaveLength(2);
  });
});

describe('describeSku', () => {
  it('names a SKU from the catalogue', () => {
    expect(describeSku(products, 12)).toEqual({
      productId: 1,
      name: 'Ethiopia Yirgacheffe',
      roast: 'Light',
      grind: 'WholeBean',
      sizeG: 1000,
    });
    expect(describeSku(products, 999)).toBeNull();
    expect(describeSku(undefined, 12)).toBeNull();
  });

  it('labels grinds', () => {
    expect(grindLabel('WholeBean')).toBe('Whole bean');
    expect(grindLabel('Espresso')).toBe('Espresso');
  });
});
