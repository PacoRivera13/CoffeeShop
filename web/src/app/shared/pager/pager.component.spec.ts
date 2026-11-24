import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MatPaginatorHarness } from '@angular/material/paginator/testing';
import { PagerComponent } from './pager.component';

describe('PagerComponent', () => {
  async function render(length: number, pageIndex = 0, pageSize = 20) {
    const fixture = TestBed.createComponent(PagerComponent);
    fixture.componentRef.setInput('length', length);
    fixture.componentRef.setInput('pageIndex', pageIndex);
    fixture.componentRef.setInput('pageSize', pageSize);
    const pageChange = vi.fn();
    fixture.componentInstance.pageChange.subscribe(pageChange);
    await fixture.whenStable();
    return { pageChange, paginator: await TestbedHarnessEnvironment.loader(fixture).getHarness(MatPaginatorHarness) };
  }

  it('shows the range of the current page', async () => {
    const { paginator } = await render(45, 1, 20);

    expect(await paginator.getRangeLabel()).toBe('21 – 40 of 45');
  });

  it('emits the next page', async () => {
    const { pageChange, paginator } = await render(45);

    await paginator.goToNextPage();

    expect(pageChange).toHaveBeenCalledExactlyOnceWith({ pageIndex: 1, pageSize: 20 });
  });

  it('emits a new page size', async () => {
    const { pageChange, paginator } = await render(45);

    await paginator.setPageSize(50);

    expect(pageChange).toHaveBeenCalledExactlyOnceWith({ pageIndex: 0, pageSize: 50 });
  });

  it('handles an empty list', async () => {
    const { paginator } = await render(0);

    expect(await paginator.getRangeLabel()).toBe('0 of 0');
    expect(await paginator.isNextPageDisabled()).toBe(true);
  });
});
