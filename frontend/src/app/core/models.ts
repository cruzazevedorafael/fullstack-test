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

/** What an active investment has earned so far and what comes next. */
export interface Accrual {
  monthlyGainRate: number;
  completedMonths: number;
  nextGainDate: IsoDate;
  nextGainAmount: number;
  withdrawalToday: Withdrawal;
}

export interface InvestmentDetails extends Investment {
  /** Null once withdrawn. */
  accrual: Accrual | null;
}

export interface GrowthPoint {
  date: IsoDate;
  balance: number;
  projected: boolean;
}

/** Rates as fractions: 0.0052 is 0.52%. */
export interface Rules {
  monthlyGainRate: number;
  taxRateUnderOneYear: number;
  taxRateOneToTwoYears: number;
  taxRateOverTwoYears: number;
}

export interface ListQuery {
  page: number;
  pageSize: number;
  ownerId?: string;
  from?: IsoDate;
  to?: IsoDate;
  status?: Investment['status'];
  sortBy?: 'CreationDate' | 'Amount' | 'Owner';
  sortDirection?: 'Asc' | 'Desc';
}

export type SummaryQuery = Pick<ListQuery, 'ownerId' | 'from' | 'to'>;

export interface Summary {
  activeBalance: number;
  activeInvestments: number;
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
