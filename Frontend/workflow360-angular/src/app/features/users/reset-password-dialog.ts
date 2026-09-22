import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { PASSWORD_VALIDATORS } from './user-dialog';
import { UserListItem, UserService } from './user.service';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const { newPassword, confirmPassword } = group.value as { newPassword: string; confirmPassword: string };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

@Component({
  selector: 'app-reset-password-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Reset password</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p class="intro">
          Set a new password for <strong>{{ user.fullName }}</strong>. They will be signed out everywhere.
        </p>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        <mat-form-field class="field">
          <mat-label>New password</mat-label>
          <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
          <mat-hint>8+ characters with upper, lower case and a number.</mat-hint>
          <mat-error>Use 8+ characters with upper, lower case and a number.</mat-error>
        </mat-form-field>
        <mat-form-field class="field">
          <mat-label>Confirm password</mat-label>
          <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
        </mat-form-field>
        @if (form.hasError('mismatch') && form.controls.confirmPassword.touched) {
          <div class="mismatch">Passwords do not match.</div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Reset password</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .intro {
      margin-top: 0;
    }
    .field {
      display: block;
      width: 100%;
    }
    .mismatch {
      color: var(--mat-sys-error);
      font-size: 12px;
    }
  `,
})
export class ResetPasswordDialog {
  private readonly userService = inject(UserService);
  private readonly dialogRef = inject(MatDialogRef<ResetPasswordDialog, boolean>);
  protected readonly user = inject<UserListItem>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      newPassword: ['', PASSWORD_VALIDATORS],
      confirmPassword: [''],
    },
    { validators: passwordsMatch },
  );

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.userService
      .resetPassword(this.user.id, this.form.controls.newPassword.value)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (error) => this.errorMessage.set(formErrorMessage(error)),
      });
  }
}
