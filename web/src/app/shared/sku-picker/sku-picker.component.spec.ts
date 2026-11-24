import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MatButtonToggleHarness } from '@angular/material/button-toggle/testing';
import { page } from 'vitest/browser';
import { SkuResponse } from '../../api/models/sku-response';
import { SkuPickerComponent } from './sku-picker.component';

const SKUS: SkuResponse[] = [
  { id: 1, grind: 'WholeBean', sizeG: 500 },
  { id: 2, grind: 'WholeBean', sizeG: 1000 },
  { id: 3, grind: 'Ground', sizeG: 500 },
];

describe('SkuPickerComponent', () => {
  async function render(skus: readonly SkuResponse[]) {
    const fixture = TestBed.createComponent(SkuPickerComponent);
    fixture.componentRef.setInput('skus', skus);
    const skuChange = vi.fn();
    fixture.componentInstance.skuChange.subscribe(skuChange);
    await fixture.whenStable();
    return { fixture, skuChange, loader: TestbedHarnessEnvironment.loader(fixture) };
  }

  it('lists the grinds and sizes of the SKUs', async () => {
    const { loader } = await render(SKUS);

    const texts = await Promise.all((await loader.getAllHarnesses(MatButtonToggleHarness)).map((t) => t.getText()));

    expect(texts).toEqual(['Whole bean', 'Ground', '500 g', '1000 g']);
  });

  it('emits the SKU once grind and size are both chosen', async () => {
    const { skuChange, loader } = await render(SKUS);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'Whole bean' }))).check();
    expect(skuChange).not.toHaveBeenCalled();
    await (await loader.getHarness(MatButtonToggleHarness.with({ text: '1000 g' }))).check();

    expect(skuChange).toHaveBeenCalledExactlyOnceWith(SKUS[1]);
  });

  it('disables sizes that do not exist for the chosen grind', async () => {
    const { loader } = await render(SKUS);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'Ground' }))).check();

    expect(await (await loader.getHarness(MatButtonToggleHarness.with({ text: '1000 g' }))).isDisabled()).toBe(true);
    expect(await (await loader.getHarness(MatButtonToggleHarness.with({ text: '500 g' }))).isDisabled()).toBe(false);
  });

  it('accepts sizes the API sends as strings', async () => {
    const { skuChange, loader } = await render([{ id: 9, grind: 'Ground', sizeG: '500' }]);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'Ground' }))).check();
    await (await loader.getHarness(MatButtonToggleHarness.with({ text: '500 g' }))).check();

    expect(skuChange).toHaveBeenCalledExactlyOnceWith({ id: 9, grind: 'Ground', sizeG: '500' });
  });

  it('shows a preselected SKU and emits the next full choice', async () => {
    const { fixture, skuChange, loader } = await render(SKUS);
    fixture.componentRef.setInput('selectedId', 2);
    await fixture.whenStable();

    expect(await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'Whole bean' }))).isChecked()).toBe(true);
    expect(await (await loader.getHarness(MatButtonToggleHarness.with({ text: '1000 g' }))).isChecked()).toBe(true);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: '500 g' }))).check();

    expect(skuChange).toHaveBeenCalledExactlyOnceWith(SKUS[0]);
  });

  it('says so when there is nothing to pick', async () => {
    await render([]);

    await expect.element(page.getByTestId('sku-picker-empty')).toHaveTextContent('No sizes available.');
  });
});
