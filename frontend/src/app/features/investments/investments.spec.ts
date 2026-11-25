import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DEFAULT_CURRENCY_CODE, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { businessToday } from '../../core/business-date';
import { GrowthPoint, Investment, InvestmentDetails, Rules, Withdrawal } from '../../core/models';
import { GrowthChart } from './growth-chart';
import { InvestmentDetail } from './investment-detail';
import { InvestmentForm } from './investment-form';
import { InvestmentList } from './investment-list';
import { WithdrawDialog } from './withdraw-dialog';

const ANA = { id: 'ana', name: 'Ana Souza', email: 'ana@example.com' };
const ACTIVE: Investment = {
  id: 'inv',
  owner: ANA,
  creationDate: '2025-01-10',
  initialAmount: 1000,
  gains: 64.22,
  expectedBalance: 1064.22,
  status: 'Active',
  withdrawal: null,
};
const WITHDRAWAL: Withdrawal = {
  date: '2026-01-10',
  grossAmount: 1064.22,
  gains: 64.22,
  taxRate: 0.185,
  taxAmount: 11.88,
  netAmount: 1052.34,
};

const DETAILS: InvestmentDetails = {
  ...ACTIVE,
  accrual: {
    monthlyGainRate: 0.0052,
    completedMonths: 12,
    nextGainDate: '2026-02-10',
    nextGainAmount: 5.53,
    withdrawalToday: WITHDRAWAL,
  },
};
const WITHDRAWN: InvestmentDetails = { ...ACTIVE, status: 'Withdrawn', withdrawal: WITHDRAWAL, accrual: null };
const GROWTH: GrowthPoint[] = [
  { date: '2025-01-10', balance: 1000, projected: false },
  { date: '2025-02-10', balance: 1005.2, projected: false },
  { date: '2025-03-10', balance: 1010.43, projected: true },
];
const RULES: Rules = {
  monthlyGainRate: 0.0052,
  taxRateUnderOneYear: 0.225,
  taxRateOneToTwoYears: 0.185,
  taxRateOverTwoYears: 0.15,
};

const dialogRef = { close: vi.fn(), disableClose: false };
let http: HttpTestingController;

beforeEach(() => {
  dialogRef.close.mockReset();
  dialogRef.disableClose = false;
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([]),
      { provide: DEFAULT_CURRENCY_CODE, useValue: 'BRL' },
      { provide: DIALOG_DATA, useValue: ACTIVE },
      { provide: DialogRef, useValue: dialogRef },
    ],
  });
  http = TestBed.inject(HttpTestingController);
});

afterEach(() => http.verify());

function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}): ComponentFixture<T> {
  const fixture = TestBed.createComponent(component);
  Object.entries(inputs).forEach(([name, value]) => fixture.componentRef.setInput(name, value));
  TestBed.tick();
  return fixture;
}

const text = (fixture: ComponentFixture<unknown>) => (fixture.nativeElement as HTMLElement).textContent ?? '';
const button = (fixture: ComponentFixture<unknown>, label: string) =>
  [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find((b) => b.textContent?.includes(label))!;

describe('InvestmentList', () => {
  const page = (items: Investment[], totalPages = 1) => ({
    items,
    page: 1,
    pageSize: 10,
    totalItems: items.length,
    totalPages,
  });

  it('asks for the page and person of the URL and shows the data', async () => {
    const fixture = render(InvestmentList, { page: '2', ownerId: 'ana' });
    http.expectOne('/api/owners').flush([ANA]);
    http.expectOne('/api/investments?page=2&pageSize=10&ownerId=ana').flush(page([ACTIVE]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ana Souza');
    expect(text(fixture)).toContain('R$1,064.22');
    expect(text(fixture)).toContain('Jan 10, 2025');
    expect(text(fixture)).toContain('Active');
  });

  it('falls back to the first page when the URL is invalid', async () => {
    const fixture = render(InvestmentList, { page: 'abc' });
    http.expectOne('/api/owners').flush([]);
    http.expectOne('/api/investments?page=1&pageSize=10').flush(page([]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('No investments yet.');
  });

  it('asks for the status and the order of the URL', async () => {
    const fixture = render(InvestmentList, { status: 'Withdrawn', sort: 'highest' });
    http.expectOne('/api/owners').flush([]);
    http
      .expectOne('/api/investments?page=1&pageSize=10&status=Withdrawn&sortBy=Amount&sortDirection=Desc')
      .flush(page([]));
    await fixture.whenStable();

    const value = (id: string) => (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>(id)!.value;
    expect(value('#status-filter')).toBe('Withdrawn');
    expect(value('#sort')).toBe('highest');
    expect(text(fixture)).toContain('No investments match these filters.');
  });

  it('ignores a status and an order that do not exist', async () => {
    const fixture = render(InvestmentList, { status: 'abc', sort: 'abc' });
    http.expectOne('/api/owners').flush([]);
    http.expectOne('/api/investments?page=1&pageSize=10').flush(page([ACTIVE]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ana Souza');
  });

  it.each([
    ['#owner-filter', 'ana', 'ownerId'],
    ['#status-filter', 'Active', 'status'],
    ['#sort', 'oldest', 'sort'],
  ])('keeps the other filters and goes back to the first page when %s changes', async (selector, value, param) => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = render(InvestmentList);
    http.expectOne('/api/owners').flush([ANA]);
    http.expectOne('/api/investments?page=1&pageSize=10').flush(page([ACTIVE]));
    await fixture.whenStable();

    const select = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));

    expect(navigate).toHaveBeenCalledWith([], {
      queryParams: { [param]: value, page: null },
      queryParamsHandling: 'merge',
    });
  });

  it('offers a retry when the API fails', async () => {
    const fixture = render(InvestmentList);
    http.expectOne('/api/owners').flush([]);
    http.expectOne('/api/investments?page=1&pageSize=10').flush(null, { status: 500, statusText: 'Error' });
    await fixture.whenStable();

    button(fixture, 'Try again').click();
    TestBed.tick();
    http.expectOne('/api/investments?page=1&pageSize=10').flush(page([ACTIVE]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ana Souza');
  });
});

describe('InvestmentDetail', () => {
  it('shows the final taxed amount of a withdrawn investment and no withdraw button', async () => {
    const fixture = render(InvestmentDetail, { id: 'inv' });
    http.expectOne('/api/investments/inv').flush(WITHDRAWN);
    http.expectOne('/api/investments/inv/growth').flush(GROWTH);
    await fixture.whenStable();

    expect(text(fixture)).toContain('R$1,052.34');
    expect(text(fixture)).toContain('18.5%');
    expect(button(fixture, 'Withdraw investment')).toBeUndefined();
  });

  it('shows the rate, the next gain and what a withdrawal today would pay', async () => {
    const fixture = render(InvestmentDetail, { id: 'inv' });
    http.expectOne('/api/investments/inv').flush(DETAILS);
    http.expectOne('/api/investments/inv/growth').flush(GROWTH);
    await fixture.whenStable();

    expect(text(fixture)).toContain('0.52%');
    expect(text(fixture)).toContain('12 months');
    expect(text(fixture)).toContain('Feb 10, 2026');
    expect(text(fixture)).toContain('R$5.53');
    expect(text(fixture)).toContain('R$11.88');
    expect(text(fixture)).toContain('R$1,052.34');
  });

  it('keeps working when only the chart fails', async () => {
    const fixture = render(InvestmentDetail, { id: 'inv' });
    http.expectOne('/api/investments/inv').flush(DETAILS);
    http.expectOne('/api/investments/inv/growth').flush(null, { status: 500, statusText: 'Error' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('R$1,064.22');
    expect(text(fixture)).toContain('The chart could not be loaded.');
  });

  it('explains when the investment does not exist', async () => {
    const fixture = render(InvestmentDetail, { id: 'x' });
    http.expectOne('/api/investments/x').flush({}, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/investments/x/growth').flush({}, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('This investment does not exist.');
  });
});

describe('GrowthChart', () => {
  const element = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;

  it('separates what was paid from the projection', async () => {
    const fixture = render(GrowthChart, { points: GROWTH });
    await fixture.whenStable();

    expect(element(fixture).querySelectorAll('svg path')).toHaveLength(2);
    expect(text(fixture)).toContain('Balance so far');
    expect(text(fixture)).toContain('Projection');
  });

  it('has no projection for a withdrawn investment', async () => {
    const fixture = render(GrowthChart, { points: GROWTH.slice(0, 2) });
    await fixture.whenStable();

    expect(element(fixture).querySelectorAll('svg path')).toHaveLength(1);
    expect(text(fixture)).not.toContain('Projection');
  });

  it('explains when there is nothing to draw', async () => {
    const fixture = render(GrowthChart, { points: GROWTH.slice(0, 1) });
    await fixture.whenStable();

    expect(element(fixture).querySelector('svg')).toBeNull();
    expect(text(fixture)).toContain('No monthly gain was paid');
  });

  it('reads each month with the keyboard', async () => {
    const fixture = render(GrowthChart, { points: GROWTH });
    const press = async (key: string) => {
      element(fixture).querySelector('[tabindex="0"]')!.dispatchEvent(new KeyboardEvent('keydown', { key }));
      await fixture.whenStable();
      return element(fixture).querySelector('[data-readout]')?.textContent ?? '';
    };

    expect(await press('Home')).toContain('R$1,000.00');
    expect(await press('ArrowRight')).toContain('R$1,005.20');
    expect(await press('End')).toContain('R$1,010.43');
    expect(await press('ArrowRight')).toContain('R$1,010.43');
    expect(await press('Escape')).toBe('');
  });

  it('offers the same values as a table', async () => {
    const fixture = render(GrowthChart, { points: GROWTH });
    button(fixture, 'Show as table').click();
    await fixture.whenStable();

    expect(element(fixture).querySelectorAll('tbody tr')).toHaveLength(3);
    expect(element(fixture).querySelector('tbody')!.textContent).toContain('Feb 10, 2025');
  });
});

describe('WithdrawDialog', () => {
  const previewUrl = (date: string) => `/api/investments/inv/withdrawal-preview?date=${date}`;

  async function chooseDate(fixture: ComponentFixture<unknown>, date: string) {
    const input = (fixture.nativeElement as HTMLElement).querySelector('input')!;
    input.value = date;
    input.dispatchEvent(new Event('input'));
    TestBed.tick();
  }

  it('previews today, lets the date change and withdraws with the chosen date', async () => {
    const fixture = render(WithdrawDialog);
    expect(button(fixture, 'Confirm').disabled).toBe(true);
    http.expectOne(previewUrl(businessToday())).flush(WITHDRAWAL);
    await fixture.whenStable();

    await chooseDate(fixture, '2026-01-10');
    expect(button(fixture, 'Confirm').disabled).toBe(true); // the old preview can't be confirmed
    http.expectOne(previewUrl('2026-01-10')).flush(WITHDRAWAL);
    await fixture.whenStable();
    expect(text(fixture)).toContain('R$1,052.34');

    button(fixture, 'Confirm').click();
    button(fixture, 'Confirm')?.click(); // a double click sends a single request
    const request = http.expectOne('/api/investments/inv/withdrawal');
    expect(request.request.body).toEqual({ date: '2026-01-10' });
    const updated = { ...ACTIVE, status: 'Withdrawn', withdrawal: WITHDRAWAL };
    request.flush(updated);

    expect(dialogRef.close).toHaveBeenCalledExactlyOnceWith(updated);
  });

  it('shows the rule broken by the date and blocks the confirmation', async () => {
    const fixture = render(WithdrawDialog);
    http
      .expectOne(previewUrl(businessToday()))
      .flush({ detail: "Can't be in the future." }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(text(fixture)).toContain("Can't be in the future.");
    expect(button(fixture, 'Confirm').disabled).toBe(true);
  });

  it('stays open with the reason when the withdrawal is refused', async () => {
    const fixture = render(WithdrawDialog);
    http.expectOne(previewUrl(businessToday())).flush(WITHDRAWAL);
    await fixture.whenStable();

    button(fixture, 'Confirm').click();
    expect(dialogRef.disableClose).toBe(true);
    http
      .expectOne('/api/investments/inv/withdrawal')
      .flush({ detail: 'Already withdrawn.' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('Already withdrawn.');
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(dialogRef.disableClose).toBe(false);
  });
});

describe('InvestmentForm', () => {
  async function fill(values: { owner?: string; amount?: string; date?: string }) {
    const fixture = render(InvestmentForm);
    http.expectOne('/api/owners').flush([ANA]);
    http.expectOne('/api/rules').flush(RULES);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const set = (selector: string, value: string | undefined, event: string) => {
      if (value !== undefined) {
        const control = element.querySelector<HTMLInputElement>(selector)!;
        control.value = value;
        control.dispatchEvent(new Event(event));
      }
    };
    set('#owner', values.owner, 'change');
    set('#amount', values.amount, 'input');
    set('#creation-date', values.date, 'input');
    TestBed.tick();
    const simulations = http.match((request) => request.url === '/api/investments/simulation');
    simulations.filter((request) => !request.cancelled).forEach((request) => request.flush(WITHDRAWAL));
    await fixture.whenStable();

    return { fixture, simulations };
  }

  async function submit(values: { owner?: string; amount?: string; date?: string }) {
    const { fixture } = await fill(values);
    button(fixture, 'Create investment').click();
    await fixture.whenStable();

    return fixture;
  }

  it('shows the monthly rate', async () => {
    const { fixture } = await fill({});

    expect(text(fixture)).toContain('0.52% per month');
  });

  it('simulates 12 months with the typed values', async () => {
    const { fixture, simulations } = await fill({ amount: '1000', date: '2025-01-10' });

    expect(simulations.at(-1)!.request.params.toString()).toBe('amount=1000&creationDate=2025-01-10&months=12');
    expect(text(fixture)).toContain('R$1,052.34');
    expect(text(fixture)).toContain('Jan 10, 2026');
  });

  it('simulates another term', async () => {
    const { fixture } = await fill({ amount: '1000', date: '2025-01-10' });

    const months = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#months')!;
    months.value = '24';
    months.dispatchEvent(new Event('input'));
    TestBed.tick();

    http
      .expectOne('/api/investments/simulation?amount=1000&creationDate=2025-01-10&months=24')
      .flush({ ...WITHDRAWAL, netAmount: 1108.04 });
    await fixture.whenStable();
    expect(text(fixture)).toContain('R$1,108.04');
  });

  it.each(['0', '601', '1.5', ''])('does not simulate a term of "%s" months', async (value) => {
    const { fixture } = await fill({ amount: '1000', date: '2025-01-10' });

    const months = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#months')!;
    months.value = value;
    months.dispatchEvent(new Event('input'));
    TestBed.tick();
    await fixture.whenStable();

    http.expectNone((request) => request.url === '/api/investments/simulation');
    expect(text(fixture)).toContain('between 1 and 600');
  });

  it('does not simulate an invalid amount', async () => {
    const { fixture, simulations } = await fill({ amount: '-1', date: '2025-01-10' });

    expect(simulations).toHaveLength(0);
    expect(text(fixture)).toContain('Enter an amount and a date');
  });

  it('sends valid data once', async () => {
    const fixture = await submit({ owner: 'ana', amount: '1000.50', date: '2025-01-10' });
    button(fixture, 'Creating').click();

    const request = http.expectOne('/api/investments');
    expect(request.request.body).toEqual({ ownerId: 'ana', amount: 1000.5, creationDate: '2025-01-10' });
  });

  it.each([
    [{ amount: '1000', date: '2025-01-10' }, 'Select the owner'],
    [{ owner: 'ana', amount: '-1' }, 'greater than zero'],
    [{ owner: 'ana', amount: '0' }, 'greater than zero'],
    [{ owner: 'ana', amount: '1.001' }, 'two decimal places'],
    [{ owner: 'ana', amount: '1', date: '2999-01-01' }, 'today or a past date'],
    [{ owner: 'ana', amount: '1', date: '' }, 'today or a past date'],
  ])('does not send %o', async (values, message) => {
    const fixture = await submit(values);

    http.expectNone('/api/investments');
    expect(text(fixture)).toContain(message);
  });
});
