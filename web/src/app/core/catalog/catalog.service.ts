import { inject, Injectable } from '@angular/core';
import { defer, Observable, shareReplay } from 'rxjs';
import { getProducts } from '../../api/fn/catalog/get-products';
import { ProductResponse } from '../../api/models/product-response';
import { ApiRx } from '../http/api-rx.service';

export interface SkuDescription {
  productId: number;
  name: string;
  roast: string;
  grind: string;
  sizeG: number;
}

/** The product catalogue, loaded once per session; a failed load is retried on the next subscription. */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly api = inject(ApiRx);

  private readonly products$ = defer(() => this.api.body(getProducts, undefined)).pipe(
    shareReplay({ bufferSize: 1, refCount: false }),
  );

  products(): Observable<ProductResponse[]> {
    return this.products$;
  }
}

/** Order lines and stock errors carry ids only; the catalogue gives them names. */
export function describeSku(products: readonly ProductResponse[] | undefined, skuId: number): SkuDescription | null {
  for (const product of products ?? []) {
    const sku = product.skus.find((s) => Number(s.id) === skuId);
    if (sku) {
      return {
        productId: Number(product.id),
        name: product.name,
        roast: product.roast,
        grind: sku.grind,
        sizeG: Number(sku.sizeG),
      };
    }
  }
  return null;
}

export const GRIND_LABELS: Readonly<Record<string, string>> = { WholeBean: 'Whole bean', Ground: 'Ground' };

export function grindLabel(grind: string): string {
  return GRIND_LABELS[grind] ?? grind;
}
