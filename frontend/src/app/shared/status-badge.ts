import { Component, input } from '@angular/core';
import { Investment } from '../core/models';

@Component({
  selector: 'app-status-badge',
  template: `
    <span
      class="inline-block rounded-full px-2.5 py-0.5 text-sm font-medium"
      [class]="status() === 'Active' ? 'bg-gain-soft text-gain' : 'bg-line text-ink'"
      >{{ status() }}</span
    >
  `,
})
export class StatusBadge {
  readonly status = input.required<Investment['status']>();
}
