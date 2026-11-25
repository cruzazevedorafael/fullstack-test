import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DEFAULT_CURRENCY_CODE, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { businessToday } from '../../core/business-date';
import { Investment, Withdrawal } from '../../core/models';
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
    http.expectOne('/api/investments/inv').flush({ ...ACTIVE, status: 'Withdrawn', withdrawal: WITHDRAWAL });
    await fixture.whenStable();

    expect(text(fixture)).toContain('R$1,052.34');
    expect(text(fixture)).toContain('18.5%');
    expect(button(fixture, 'Withdraw investment')).toBeUndefined();
  });

  it('explains when the investment does not exist', async () => {
    const fixture = render(InvestmentDetail, { id: 'x' });
    http.expectOne('/api/investments/x').flush({}, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('This investment does not exist.');
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
    button(fixture, 'Create investment').click();
    await fixture.whenStable();

    return fixture;
  }

  it('sends valid data once', async () => {
    const fixture = await fill({ owner: 'ana', amount: '1000.50', date: '2025-01-10' });
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
    const fixture = await fill(values);

    http.expectNone('/api/investments');
    expect(text(fixture)).toContain(message);
  });
});
