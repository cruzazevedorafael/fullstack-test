import { HttpClient, HttpResourceRequest } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfig } from './app-config';
import { Investment, IsoDate, NewInvestment, NewOwner, Owner } from './models';

/** Single place that knows the API routes. Reads are described as requests for httpResource. */
@Injectable({ providedIn: 'root' })
export class InvestmentsApi {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfig);

  listRequest(page: number, pageSize: number, ownerId?: string): HttpResourceRequest {
    return {
      url: this.url('/investments'),
      params: { page, pageSize, ...(ownerId ? { ownerId } : {}) },
    };
  }

  detailRequest(id: string): HttpResourceRequest {
    return { url: this.url(`/investments/${encodeURIComponent(id)}`) };
  }

  withdrawalPreviewRequest(id: string, date: IsoDate): HttpResourceRequest {
    return { url: this.url(`/investments/${encodeURIComponent(id)}/withdrawal-preview`), params: { date } };
  }

  ownersRequest(): HttpResourceRequest {
    return { url: this.url('/owners') };
  }

  createOwner(owner: NewOwner): Observable<Owner> {
    return this.http.post<Owner>(this.url('/owners'), owner);
  }

  createInvestment(investment: NewInvestment): Observable<Investment> {
    return this.http.post<Investment>(this.url('/investments'), investment);
  }

  withdraw(id: string, date: IsoDate): Observable<Investment> {
    return this.http.post<Investment>(this.url(`/investments/${encodeURIComponent(id)}/withdrawal`), { date });
  }

  private url(path: string): string {
    return `${this.config.apiUrl}/api${path}`;
  }
}
