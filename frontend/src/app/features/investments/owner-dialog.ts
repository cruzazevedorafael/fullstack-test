import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { errorMessage } from '../../core/error-message';
import { InvestmentsApi } from '../../core/investments-api';
import { Owner } from '../../core/models';

@Component({
  selector: 'app-owner-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './owner-dialog.html',
})
export class OwnerDialog {
  private readonly api = inject(InvestmentsApi);
  private readonly ref = inject<DialogRef<Owner>>(DialogRef);

  protected readonly submitting = signal(false);
  protected readonly error = signal('');
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(120), Validators.pattern(/\S/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set('');
    this.ref.disableClose = true;

    this.api.createOwner(this.form.getRawValue()).subscribe({
      next: (owner) => this.ref.close(owner),
      error: (error) => {
        this.error.set(errorMessage(error));
        this.submitting.set(false);
        this.ref.disableClose = false;
      },
    });
  }

  protected cancel(): void {
    this.ref.close();
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && control.touched;
  }
}
