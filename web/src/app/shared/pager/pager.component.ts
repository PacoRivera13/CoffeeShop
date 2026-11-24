import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';

export interface PageChange {
  /** Zero-based page index. */
  pageIndex: number;
  pageSize: number;
}

@Component({
  selector: 'app-pager',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatPaginatorModule],
  template: `
    <mat-paginator
      aria-label="Pages"
      data-testid="pager"
      [length]="length()"
      [pageIndex]="pageIndex()"
      [pageSize]="pageSize()"
      [pageSizeOptions]="pageSizeOptions()"
      (page)="onPage($event)"
    />
  `,
  styles: `
    mat-paginator {
      background: transparent;
    }
  `,
})
export class PagerComponent {
  readonly length = input.required<number>();
  readonly pageIndex = input(0);
  readonly pageSize = input(20);
  readonly pageSizeOptions = input<number[]>([10, 20, 50]);
  readonly pageChange = output<PageChange>();

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ pageIndex: event.pageIndex, pageSize: event.pageSize });
  }
}
