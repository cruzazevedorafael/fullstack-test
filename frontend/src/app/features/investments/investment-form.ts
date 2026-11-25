import { Dialog } from '@angular/cdk/dialog';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { businessToday } from '../../core/business-date';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { Owner } from '../../core/models';
import { ToastService } from '../../shared/toaster';
import { InvestmentSimulator } from './investment-simulator';
import { OwnerDialog } from './owner-dialog';

const MAX_AMOUNT = 999_999_999_999.99;
const MIN_DATE = '1900-01-01';

@Component({
  selector: 'app-investment-form',
  imports: [InvestmentSimulator, ReactiveFormsModule, RouterLink],
  templateUrl: './investment-form.html',
})
export class InvestmentForm {
  private readonly api = inject(InvestmentsApi);
  private readonly dialog = inject(Dialog);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly today = businessToday();
  protected readonly minDate = MIN_DATE;
  protected readonly owners = httpResource<Owner[]>(() => this.api.ownersRequest());
  protected readonly submitting = signal(false);
  protected readonly error = signal('');

  protected readonly form = inject(NonNullableFormBuilder).group({
    ownerId: ['', Validators.required],
    amount: [
      null as number | null,
      [Validators.required, Validators.min(0.01), Validators.max(MAX_AMOUNT), twoDecimals],
    ],
    creationDate: [this.today, [Validators.required, dateBetween(MIN_DATE, this.today)]],
  });

  private readonly values = toSignal(this.form.valueChanges);
  protected readonly terms = computed(() => {
    this.values();
    const { amount, creationDate } = this.form.controls;
    return amount.valid && creationDate.valid && amount.value !== null
      ? { amount: amount.value, creationDate: creationDate.value }
      : null;
  });

  protected addOwner(): void {
    const ref = this.dialog.open<Owner>(OwnerDialog, {
      ariaLabelledBy: 'owner-title',
      width: 'min(28rem, calc(100vw - 2rem))',
    });

    ref.closed.subscribe((owner) => {
      if (owner) {
        const current = this.owners.hasValue() ? this.owners.value() : [];
        this.owners.set([...current, owner]);
        this.form.controls.ownerId.setValue(owner.id);
      }
    });
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    const { ownerId, amount, creationDate } = this.form.getRawValue();
    if (this.form.invalid || amount === null || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.api.createInvestment({ ownerId, amount, creationDate }).subscribe({
      next: (investment) => {
        this.toast.show('Investment created');
        this.router.navigate(['/investments', investment.id]);
      },
      error: (error) => {
        this.error.set(errorMessage(error));
        this.submitting.set(false);
      },
    });
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && control.touched;
  }
}

function twoDecimals({ value }: AbstractControl<number | null>): ValidationErrors | null {
  return value === null || /^-?\d+(\.\d{1,2})?$/.test(String(value)) ? null : { twoDecimals: true };
}

/** ISO dates sort alphabetically, so plain string comparison is enough. */
function dateBetween(min: string, max: string) {
  return ({ value }: AbstractControl<string>): ValidationErrors | null =>
    !value || (value >= min && value <= max) ? null : { dateBetween: true };
}
