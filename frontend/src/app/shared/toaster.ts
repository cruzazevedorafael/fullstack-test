import { Component, inject, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly message = signal('');
  private timer?: ReturnType<typeof setTimeout>;

  show(message: string): void {
    clearTimeout(this.timer);
    this.message.set(message);
    this.timer = setTimeout(() => this.message.set(''), 5000);
  }
}

@Component({
  selector: 'app-toaster',
  template: `
    <div role="status" class="pointer-events-none fixed inset-x-0 bottom-6 flex justify-center px-4">
      @if (toast.message(); as message) {
        <p class="rounded-md bg-ink px-4 py-3 text-bg shadow-lg">{{ message }}</p>
      }
    </div>
  `,
})
export class Toaster {
  protected readonly toast = inject(ToastService);
}
