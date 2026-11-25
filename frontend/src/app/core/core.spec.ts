import { HttpErrorResponse } from '@angular/common/http';
import { businessToday } from './business-date';
import { errorMessage } from './error-message';

describe('businessToday', () => {
  it('uses the São Paulo calendar day, not the UTC one', () => {
    expect(businessToday(new Date('2026-06-16T01:30:00Z'))).toBe('2026-06-15');
    expect(businessToday(new Date('2026-06-16T03:30:00Z'))).toBe('2026-06-16');
  });
});

describe('errorMessage', () => {
  const http = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

  it('shows the detail sent by the API', () => {
    expect(errorMessage(http(409, { detail: 'Already withdrawn.' }))).toBe('Already withdrawn.');
  });

  it('joins field validation errors', () => {
    expect(errorMessage(http(400, { errors: { Amount: ['A.'], Date: ['B.'] } }))).toBe('A. B.');
  });

  it('explains connection failures', () => {
    expect(errorMessage(http(0, null))).toContain('could not be reached');
  });

  it('never exposes server error details', () => {
    expect(errorMessage(http(500, { detail: 'stack trace' }))).not.toContain('stack');
    expect(errorMessage(http(502, '<html>'))).toContain('Something went wrong');
    expect(errorMessage(new Error('boom'))).not.toContain('boom');
  });
});
