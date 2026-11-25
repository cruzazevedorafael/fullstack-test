import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { IsoDate, Rules, Withdrawal } from '../../core/models';

const MAX_MONTHS = 600;

/** Shows what an investment would pay after some months. Nothing is saved. */
@Component({
  selector: 'app-investment-simulator',
  imports: [CurrencyPipe, DatePipe, PercentPipe],
  templateUrl: './investment-simulator.html',
})
export class InvestmentSimulator {
  /** Null while the amount or the date typed in the form is invalid. */
  readonly terms = input.required<{ amount: number; creationDate: IsoDate } | null>();

  private readonly api = inject(InvestmentsApi);

  protected readonly maxMonths = MAX_MONTHS;
  protected readonly months = signal('12');
  protected readonly validMonths = computed(() => {
    const months = Number(this.months());
    return Number.isInteger(months) && months >= 1 && months <= MAX_MONTHS ? months : null;
  });

  protected readonly rules = httpResource<Rules>(() => this.api.rulesRequest());
  protected readonly result = httpResource<Withdrawal>(() => {
    const terms = this.terms();
    const months = this.validMonths();
    return terms && months ? this.api.simulationRequest(terms.amount, terms.creationDate, months) : undefined;
  });
  protected readonly error = computed(() => {
    const error = this.result.error();
    return error ? errorMessage(error) : '';
  });
}
