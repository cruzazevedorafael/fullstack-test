import { HttpClient, HttpResourceRequest } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfig } from './app-config';
import { InvestmentDetails, IsoDate, ListQuery, NewInvestment, NewOwner, Owner } from './models';

/** Single place that knows the API routes. Reads are described as requests for httpResource. */
@Injectable({ providedIn: 'root' })
export class InvestmentsApi {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfig);

  listRequest(query: ListQuery): HttpResourceRequest {
    const params = Object.fromEntries(Object.entries(query).filter(([, value]) => value));
    return { url: this.url('/investments'), params };
  }

  detailRequest(id: string): HttpResourceRequest {
    return { url: this.url(`/investments/${encodeURIComponent(id)}`) };
  }

  growthRequest(id: string): HttpResourceRequest {
    return { url: this.url(`/investments/${encodeURIComponent(id)}/growth`) };
  }

  simulationRequest(amount: number, creationDate: IsoDate, months: number): HttpResourceRequest {
    return { url: this.url('/investments/simulation'), params: { amount, creationDate, months } };
  }

  rulesRequest(): HttpResourceRequest {
    return { url: this.url('/rules') };
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

  createInvestment(investment: NewInvestment): Observable<InvestmentDetails> {
    return this.http.post<InvestmentDetails>(this.url('/investments'), investment);
  }

  withdraw(id: string, date: IsoDate): Observable<InvestmentDetails> {
    return this.http.post<InvestmentDetails>(this.url(`/investments/${encodeURIComponent(id)}/withdrawal`), { date });
  }

  private url(path: string): string {
    return `${this.config.apiUrl}/api${path}`;
  }
}
