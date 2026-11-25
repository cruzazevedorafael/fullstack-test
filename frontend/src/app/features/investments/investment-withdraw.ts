import { CurrencyPipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { businessToday } from '../../core/business-date';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { InvestmentDetails, Withdrawal } from '../../core/models';
import { ToastService } from '../../shared/toaster';

/** Shows the simulated withdrawal for the chosen date and only then lets the user confirm it. */
@Component({
  selector: 'app-investment-withdraw',
  imports: [CurrencyPipe, PercentPipe, RouterLink],
  templateUrl: './investment-withdraw.html',
})
export class InvestmentWithdraw {
  readonly id = input.required<string>();

  private readonly api = inject(InvestmentsApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly today = businessToday();
  protected readonly date = signal(this.today);
  protected readonly submitting = signal(false);
  private readonly submitError = signal('');

  protected readonly investment = httpResource<InvestmentDetails>(() => this.api.detailRequest(this.id()));
  protected readonly notFound = computed(() => this.investment.statusCode() === 404);
  protected readonly loadError = computed(() => {
    const error = this.investment.error();
    return error ? errorMessage(error) : '';
  });

  private readonly active = computed(() => this.investment.hasValue() && this.investment.value().status === 'Active');
  protected readonly preview = httpResource<Withdrawal>(() =>
    this.active() && this.date() ? this.api.withdrawalPreviewRequest(this.id(), this.date()) : undefined,
  );
  protected readonly error = computed(() => {
    const error = this.preview.error();
    return this.submitError() || (error ? errorMessage(error) : '');
  });
  protected readonly canConfirm = computed(
    () => this.preview.hasValue() && !this.preview.isLoading() && !this.submitting(),
  );

  protected changeDate(date: string): void {
    this.submitError.set('');
    this.date.set(date);
  }

  protected confirm(): void {
    if (!this.canConfirm()) {
      return;
    }

    this.submitting.set(true);
    this.submitError.set('');

    this.api.withdraw(this.id(), this.date()).subscribe({
      next: ({ id }) => {
        this.toast.show('Investment withdrawn');
        this.router.navigate(['/investments', id]);
      },
      error: (error) => {
        this.submitError.set(errorMessage(error));
        this.submitting.set(false);
      },
    });
  }
}
