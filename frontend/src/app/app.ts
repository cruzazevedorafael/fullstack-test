import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { Toaster } from './shared/toaster';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet, Toaster],
  template: `
    <header class="border-b border-line bg-surface">
      <div class="mx-auto flex max-w-5xl items-center px-4 py-4">
        <a routerLink="/investments" class="text-lg font-semibold">Investments</a>
      </div>
    </header>
    <main class="mx-auto max-w-5xl px-4 py-8">
      <router-outlet />
    </main>
    <app-toaster />
  `,
})
export class App {}
