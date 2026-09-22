import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { toIsoDate } from '../../shared/dates';
import { Holiday } from './leave.models';
import { LeaveService } from './leave.service';

@Component({
  selector: 'app-holiday-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Add holiday</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        <mat-form-field class="field">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="date" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-error>Date is required.</mat-error>
        </mat-form-field>
        <mat-form-field class="field">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="100" placeholder="e.g. Victory Day" />
          <mat-error>Name is required.</mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Add</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .field {
      display: block;
      width: 100%;
    }
  `,
})
export class HolidayDialog {
  private readonly leaveService = inject(LeaveService);
  private readonly dialogRef = inject(MatDialogRef<HolidayDialog, Holiday>);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    date: [null as Date | null, Validators.required],
    name: ['', [Validators.required, Validators.maxLength(100)]],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { date, name } = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    this.leaveService
      .addHoliday(toIsoDate(date!), name)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (holiday) => this.dialogRef.close(holiday),
        error: (error) => this.errorMessage.set(formErrorMessage(error)),
      });
  }
}
