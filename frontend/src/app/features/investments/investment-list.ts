import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { Investment, ListQuery, Owner, Page } from '../../core/models';
import { StatusBadge } from '../../shared/status-badge';

const PAGE_SIZE = 10;

type Sort = Pick<ListQuery, 'sortBy' | 'sortDirection'>;

const STATUSES: Investment['status'][] = ['Active', 'Withdrawn'];
const DEFAULT_SORT = 'newest';
const SORTS: Record<string, { label: string; query: Sort }> = {
  newest: { label: 'Newest first', query: {} },
  oldest: { label: 'Oldest first', query: { sortBy: 'CreationDate', sortDirection: 'Asc' } },
  highest: { label: 'Highest amount', query: { sortBy: 'Amount', sortDirection: 'Desc' } },
  lowest: { label: 'Lowest amount', query: { sortBy: 'Amount', sortDirection: 'Asc' } },
  owner: { label: 'Owner, A to Z', query: { sortBy: 'Owner', sortDirection: 'Asc' } },
  'owner-desc': { label: 'Owner, Z to A', query: { sortBy: 'Owner', sortDirection: 'Desc' } },
};

/** The page, the filters and the order live in the URL, so they survive a reload and the back button. */
@Component({
  selector: 'app-investment-list',
  imports: [CurrencyPipe, DatePipe, RouterLink, StatusBadge],
  templateUrl: './investment-list.html',
})
export class InvestmentList {
  readonly page = input(1, { transform: toPage });
  readonly ownerId = input<string>();
  readonly status = input(undefined, { transform: toStatus });
  readonly sort = input(DEFAULT_SORT, { transform: toSort });

  private readonly api = inject(InvestmentsApi);
  private readonly router = inject(Router);

  protected readonly statuses = STATUSES;
  protected readonly sorts = Object.entries(SORTS).map(([id, { label }]) => ({ id, label }));
  protected readonly skeletonRows = [1, 2, 3, 4];
  protected readonly owners = httpResource<Owner[]>(() => this.api.ownersRequest());
  protected readonly list = httpResource<Page<Investment>>(() =>
    this.api.listRequest({
      page: this.page(),
      pageSize: PAGE_SIZE,
      ownerId: this.ownerId(),
      status: this.status(),
      ...SORTS[this.sort()].query,
    }),
  );
  protected readonly filtered = computed(() => !!this.ownerId() || !!this.status());
  protected readonly error = computed(() => {
    const error = this.list.error();
    return error ? errorMessage(error) : '';
  });

  protected change(param: 'ownerId' | 'status' | 'sort', value: string): void {
    this.router.navigate([], { queryParams: { [param]: value || null, page: null }, queryParamsHandling: 'merge' });
  }

  protected clearFilters(): void {
    this.router.navigate([], {
      queryParams: { ownerId: null, status: null, page: null },
      queryParamsHandling: 'merge',
    });
  }

  protected goTo(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }
}

function toStatus(value: unknown): Investment['status'] | undefined {
  return STATUSES.find((status) => status === value);
}

function toSort(value: unknown): string {
  return typeof value === 'string' && Object.hasOwn(SORTS, value) ? value : DEFAULT_SORT;
}

function toPage(value: unknown): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}
