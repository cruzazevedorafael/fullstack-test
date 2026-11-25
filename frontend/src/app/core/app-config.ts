import { Injectable } from '@angular/core';

/** Settings read when the app starts, so the same build can point to any API. */
@Injectable({ providedIn: 'root' })
export class AppConfig {
  apiUrl = '';

  async load(): Promise<void> {
    const response = await fetch('config.json', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`config.json could not be loaded (HTTP ${response.status})`);
    }

    const { apiUrl } = (await response.json()) as { apiUrl: string };
    this.apiUrl = apiUrl.replace(/\/+$/, '');
  }
}
