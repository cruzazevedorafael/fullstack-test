import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { GrowthPoint, InvestmentDetails } from '../../core/models';
import { StatusBadge } from '../../shared/status-badge';
import { GrowthChart } from './growth-chart';

@Component({
  selector: 'app-investment-detail',
  imports: [CurrencyPipe, DatePipe, GrowthChart, PercentPipe, RouterLink, StatusBadge],
  templateUrl: './investment-detail.html',
})
export class InvestmentDetail {
  readonly id = input.required<string>();

  private readonly api = inject(InvestmentsApi);

  protected readonly investment = httpResource<InvestmentDetails>(() => this.api.detailRequest(this.id()));
  protected readonly growth = httpResource<GrowthPoint[]>(() => this.api.growthRequest(this.id()));
  protected readonly notFound = computed(() => this.investment.statusCode() === 404);
  protected readonly error = computed(() => {
    const error = this.investment.error();
    return error ? errorMessage(error) : '';
  });
}
