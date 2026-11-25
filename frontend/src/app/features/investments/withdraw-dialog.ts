import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { CurrencyPipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { businessToday } from '../../core/business-date';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { Investment, Withdrawal } from '../../core/models';

/** Shows the simulated withdrawal for the chosen date and only then lets the user confirm it. */
@Component({
  selector: 'app-withdraw-dialog',
  imports: [CurrencyPipe, PercentPipe],
  templateUrl: './withdraw-dialog.html',
})
export class WithdrawDialog {
  private readonly api = inject(InvestmentsApi);
  private readonly ref = inject<DialogRef<Investment>>(DialogRef);
  protected readonly investment = inject<Investment>(DIALOG_DATA);

  protected readonly today = businessToday();
  protected readonly date = signal(this.today);
  protected readonly submitting = signal(false);
  private readonly submitError = signal('');

  protected readonly preview = httpResource<Withdrawal>(() =>
    this.date() ? this.api.withdrawalPreviewRequest(this.investment.id, this.date()) : undefined,
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
    // Closing with Escape or the backdrop would hide the result of a withdrawal in progress.
    this.ref.disableClose = true;

    this.api.withdraw(this.investment.id, this.date()).subscribe({
      next: (updated) => this.ref.close(updated),
      error: (error) => {
        this.submitError.set(errorMessage(error));
        this.submitting.set(false);
        this.ref.disableClose = false;
      },
    });
  }

  protected cancel(): void {
    this.ref.close();
  }
}
