import { AbstractControl } from '@angular/forms';

/** First error message for a control, used inside <mat-error>. */
export function fieldError(control: AbstractControl): string {
  const errors = control.errors;
  if (!errors) {
    return '';
  }
  if (errors['server']) {
    return errors['server'];
  }
  if (errors['required']) {
    return 'This field is required.';
  }
  if (errors['email']) {
    return 'Enter a valid email address.';
  }
  if (errors['maxlength']) {
    return `Must be ${errors['maxlength'].requiredLength} characters or fewer.`;
  }
  if (errors['minlength']) {
    return `Must be at least ${errors['minlength'].requiredLength} characters.`;
  }
  if (errors['pattern']) {
    return 'The format is not valid.';
  }
  return 'The value is not valid.';
}
