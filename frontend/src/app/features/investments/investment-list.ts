import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { Investment, Owner, Page } from '../../core/models';
import { StatusBadge } from '../../shared/status-badge';

const PAGE_SIZE = 10;

/** The page and the filter live in the URL, so they survive a reload and the back button. */
@Component({
  selector: 'app-investment-list',
  imports: [CurrencyPipe, DatePipe, RouterLink, StatusBadge],
  templateUrl: './investment-list.html',
})
export class InvestmentList {
  readonly page = input(1, { transform: toPage });
  readonly ownerId = input<string>();

  private readonly api = inject(InvestmentsApi);
  private readonly router = inject(Router);

  protected readonly skeletonRows = [1, 2, 3, 4];
  protected readonly owners = httpResource<Owner[]>(() => this.api.ownersRequest());
  protected readonly list = httpResource<Page<Investment>>(() =>
    this.api.listRequest(this.page(), PAGE_SIZE, this.ownerId()),
  );
  protected readonly error = computed(() => {
    const error = this.list.error();
    return error ? errorMessage(error) : '';
  });

  protected filterBy(ownerId: string): void {
    this.router.navigate([], { queryParams: { ownerId: ownerId || null, page: null } });
  }

  protected goTo(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }
}

function toPage(value: unknown): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}
