import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

/** Shape of the ProblemDetails / ValidationProblemDetails responses returned by the API. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function getErrorMessage(
  error: unknown,
  fallback = 'Something went wrong. Please try again.',
): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = error.error as ProblemDetails | null;
  if (problem?.detail) {
    return problem.detail;
  }
  if (problem?.errors) {
    return Object.values(problem.errors).flat()[0] ?? fallback;
  }
  return problem?.title ?? fallback;
}

/**
 * Message to show inside a form or dialog. Returns null for errors the error interceptor
 * already shows as a toast (offline, 403, 429, 5xx), so the user doesn't see them twice.
 */
export function formErrorMessage(error: unknown): string | null {
  if (error instanceof HttpErrorResponse && [400, 404, 409].includes(error.status)) {
    return getErrorMessage(error);
  }
  return null;
}

/**
 * Puts API validation errors on the matching form controls ("EmployeeCode" -> employeeCode).
 * Returns false when there was nothing to map, so the caller can show a general message instead.
 */
export function applyServerErrors(form: FormGroup, error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return false;
  }

  const errors = (error.error as ProblemDetails | null)?.errors;
  if (!errors) {
    return false;
  }

  let applied = false;
  for (const [field, messages] of Object.entries(errors)) {
    const control = form.get(field.charAt(0).toLowerCase() + field.slice(1));
    if (control) {
      control.setErrors({ server: messages[0] });
      control.markAsTouched();
      applied = true;
    }
  }
  return applied;
}
