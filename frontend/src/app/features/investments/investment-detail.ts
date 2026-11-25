import { Dialog } from '@angular/cdk/dialog';
import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { GrowthPoint, InvestmentDetails } from '../../core/models';
import { StatusBadge } from '../../shared/status-badge';
import { ToastService } from '../../shared/toaster';
import { GrowthChart } from './growth-chart';
import { WithdrawDialog } from './withdraw-dialog';

@Component({
  selector: 'app-investment-detail',
  imports: [CurrencyPipe, DatePipe, GrowthChart, PercentPipe, RouterLink, StatusBadge],
  templateUrl: './investment-detail.html',
})
export class InvestmentDetail {
  readonly id = input.required<string>();

  private readonly api = inject(InvestmentsApi);
  private readonly dialog = inject(Dialog);
  private readonly toast = inject(ToastService);

  protected readonly investment = httpResource<InvestmentDetails>(() => this.api.detailRequest(this.id()));
  protected readonly growth = httpResource<GrowthPoint[]>(() => this.api.growthRequest(this.id()));
  protected readonly notFound = computed(() => this.investment.statusCode() === 404);
  protected readonly error = computed(() => {
    const error = this.investment.error();
    return error ? errorMessage(error) : '';
  });

  protected openWithdrawal(investment: InvestmentDetails): void {
    const ref = this.dialog.open<InvestmentDetails>(WithdrawDialog, {
      data: investment,
      ariaLabelledBy: 'withdraw-title',
      width: 'min(28rem, calc(100vw - 2rem))',
    });

    ref.closed.subscribe((updated) => {
      if (updated) {
        this.investment.set(updated);
        this.growth.reload();
        this.toast.show('Investment withdrawn');
      }
    });
  }
}
