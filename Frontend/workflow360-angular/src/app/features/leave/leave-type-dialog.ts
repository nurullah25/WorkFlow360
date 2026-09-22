import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { finalize } from 'rxjs';

import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { fieldError } from '../../shared/form-errors';
import { LeaveType } from './leave.models';
import { LeaveService } from './leave.service';

@Component({
  selector: 'app-leave-type-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatSlideToggleModule],
  template: `
    <h2 mat-dialog-title>{{ leaveType ? 'Edit leave type' : 'Add leave type' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        @if (!leaveType) {
          <div class="form-hint-banner">Every current employee gets this allocation for the current year.</div>
        }
        <div class="form-grid">
          <mat-form-field>
            <mat-label>Code</mat-label>
            <input matInput formControlName="code" maxlength="10" />
            <mat-error>{{ fieldError(form.controls.code) }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Days per year</mat-label>
            <input matInput type="number" formControlName="defaultDaysPerYear" min="0" max="365" step="0.5" />
            <mat-error>{{ fieldError(form.controls.defaultDaysPerYear) }}</mat-error>
          </mat-form-field>
          <mat-form-field class="full-width">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" maxlength="50" />
            <mat-error>{{ fieldError(form.controls.name) }}</mat-error>
          </mat-form-field>
          <mat-slide-toggle formControlName="isPaid">Paid leave</mat-slide-toggle>
          @if (leaveType) {
            <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class LeaveTypeDialog {
  private readonly leaveService = inject(LeaveService);
  private readonly dialogRef = inject(MatDialogRef<LeaveTypeDialog, LeaveType>);
  protected readonly leaveType = inject<LeaveType | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    code: [this.leaveType?.code ?? '', [Validators.required, Validators.maxLength(10)]],
    name: [this.leaveType?.name ?? '', [Validators.required, Validators.maxLength(50)]],
    defaultDaysPerYear: [this.leaveType?.defaultDaysPerYear ?? 0, [Validators.required, Validators.min(0), Validators.max(365)]],
    isPaid: [this.leaveType?.isPaid ?? true],
    isActive: [this.leaveType?.isActive ?? true],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.leaveService
      .saveLeaveType(this.leaveType?.id ?? null, this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (saved) => this.dialogRef.close(saved),
        error: (error) => {
          if (!applyServerErrors(this.form, error)) {
            this.errorMessage.set(formErrorMessage(error));
          }
        },
      });
  }
}
