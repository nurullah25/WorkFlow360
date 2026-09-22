import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { catchError, debounceTime, finalize, map, of, switchMap } from 'rxjs';

import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { toIsoDate } from '../../shared/dates';
import { fieldError } from '../../shared/form-errors';
import { LeavePreview, LeaveRequest } from './leave.models';
import { LeaveService } from './leave.service';

@Component({
  selector: 'app-request-leave-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Request leave</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }

        <mat-form-field class="field">
          <mat-label>Leave type</mat-label>
          <mat-select formControlName="leaveTypeId">
            @for (type of leaveTypes(); track type.id) {
              <mat-option [value]="type.id">{{ type.name }}{{ type.isPaid ? '' : ' (unpaid)' }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ fieldError(form.controls.leaveTypeId) }}</mat-error>
        </mat-form-field>

        <mat-form-field class="field">
          <mat-label>Dates</mat-label>
          <mat-date-range-input [rangePicker]="picker" [min]="today">
            <input matStartDate formControlName="startDate" placeholder="First day" />
            <input matEndDate formControlName="endDate" placeholder="Last day" />
          </mat-date-range-input>
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-date-range-picker #picker />
          <mat-error>Choose the first and last day of your leave.</mat-error>
        </mat-form-field>

        @if (preview(); as preview) {
          <div class="preview" [class.over]="exceedsBalance()">
            <strong>{{ preview.workingDays }} working day(s)</strong>
            @if (preview.availableDays !== null) {
              · {{ preview.availableDays }} available
            }
            @if (exceedsBalance()) {
              <div>This is more than your available balance.</div>
            }
          </div>
        }

        <mat-form-field class="field">
          <mat-label>Reason</mat-label>
          <textarea matInput formControlName="reason" rows="3" maxlength="500"></textarea>
          <mat-error>{{ fieldError(form.controls.reason) }}</mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Submit request</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .field {
      display: block;
      width: 100%;
    }
    mat-dialog-content {
      padding-top: 8px !important;
    }
    .preview {
      margin: -4px 0 16px;
      padding: 8px 12px;
      border-radius: 6px;
      background: #e6f0fb;
      color: #1d5a9c;

      &.over {
        background: #fbe9ea;
        color: #8a1c21;
      }
    }
  `,
})
export class RequestLeaveDialog {
  private readonly leaveService = inject(LeaveService);
  private readonly dialogRef = inject(MatDialogRef<RequestLeaveDialog, LeaveRequest>);

  protected readonly fieldError = fieldError;
  protected readonly today = new Date();
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly leaveTypes = toSignal(this.leaveService.leaveTypes(), { initialValue: [] });

  protected readonly form = inject(FormBuilder).nonNullable.group({
    leaveTypeId: [null as number | null, Validators.required],
    startDate: [null as Date | null, Validators.required],
    endDate: [null as Date | null, Validators.required],
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  protected readonly preview = toSignal(
    this.form.valueChanges.pipe(
      debounceTime(300),
      map(() => this.form.getRawValue()),
      switchMap(({ leaveTypeId, startDate, endDate }) =>
        leaveTypeId && startDate && endDate
          ? this.leaveService
              .preview(leaveTypeId, toIsoDate(startDate), toIsoDate(endDate))
              .pipe(catchError(() => of(null)))
          : of(null),
      ),
    ),
    { initialValue: null as LeavePreview | null },
  );

  protected readonly exceedsBalance = computed(() => {
    const preview = this.preview();
    return !!preview && preview.availableDays !== null && preview.workingDays > preview.availableDays;
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    this.leaveService
      .submit({
        leaveTypeId: value.leaveTypeId!,
        startDate: toIsoDate(value.startDate!),
        endDate: toIsoDate(value.endDate!),
        reason: value.reason,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (request) => this.dialogRef.close(request),
        error: (error) => {
          if (!applyServerErrors(this.form, error)) {
            this.errorMessage.set(formErrorMessage(error));
          }
        },
      });
  }
}
