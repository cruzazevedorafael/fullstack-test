import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Turns any failure into a sentence that can be shown to the user. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'The server could not be reached. Check your connection and try again.';
    }

    const problem = error.error as ProblemDetails | null;
    const validation = Object.values(problem?.errors ?? {}).flat();
    const message = validation.join(' ') || problem?.detail;
    if (message && error.status < 500) {
      return message;
    }
  }

  return 'Something went wrong. Try again in a moment.';
}
