import { IsoDate } from './models';

/** Current day in the business time zone, the same one used by the API. */
export function businessToday(now = new Date()): IsoDate {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'America/Sao_Paulo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}
