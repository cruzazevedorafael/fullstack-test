/** Dates travel as yyyy-MM-dd strings: they are calendar days, without time or time zone. */
export type IsoDate = string;

export interface Owner {
  id: string;
  name: string;
  email: string;
}

export interface Withdrawal {
  date: IsoDate;
  grossAmount: number;
  gains: number;
  taxRate: number;
  taxAmount: number;
  netAmount: number;
}

export interface Investment {
  id: string;
  owner: Owner;
  creationDate: IsoDate;
  initialAmount: number;
  gains: number;
  expectedBalance: number;
  status: 'Active' | 'Withdrawn';
  withdrawal: Withdrawal | null;
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface NewOwner {
  name: string;
  email: string;
}

export interface NewInvestment {
  ownerId: string;
  amount: number;
  creationDate: IsoDate;
}
