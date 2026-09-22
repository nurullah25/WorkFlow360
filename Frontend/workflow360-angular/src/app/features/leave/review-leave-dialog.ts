import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { LeaveRequest } from './leave.models';
import { LeaveService } from './leave.service';

export interface ReviewLeaveData {
  request: LeaveRequest;
  decision: 'approve' | 'reject';
}

@Component({
  selector: 'app-review-leave-dialog',
  imports: [DatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ isReject ? 'Reject leave request' : 'Approve leave request' }}</h2>
    <mat-dialog-content>
      @if (errorMessage()) {
        <div class="form-error-banner">{{ errorMessage() }}</div>
      }
      <dl class="summary">
        <dt>Employee</dt>
        <dd>{{ request.employeeName }} ({{ request.employeeCode }})</dd>
        <dt>Leave</dt>
        <dd>
          {{ request.leaveTypeName }}, {{ request.totalDays }} day(s):
          {{ request.startDate | date: 'mediumDate' }} – {{ request.endDate | date: 'mediumDate' }}
        </dd>
        <dt>Reason</dt>
        <dd>{{ request.reason }}</dd>
      </dl>
      <mat-form-field class="field">
        <mat-label>{{ isReject ? 'Reason for rejecting' : 'Comment (optional)' }}</mat-label>
        <textarea matInput [formControl]="comment" rows="3" maxlength="500"></textarea>
        @if (isReject) {
          <mat-hint>The employee will see this.</mat-hint>
        }
        <mat-error>Please tell the employee why.</mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [class.reject]="isReject" [disabled]="saving()" (click)="save()">
        {{ isReject ? 'Reject' : 'Approve' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .summary {
      margin: 0 0 16px;
      dt {
        font-size: 12px;
        color: var(--wf-text-muted);
        margin-top: 8px;
      }
      dd {
        margin: 2px 0 0;
      }
    }
    .field {
      display: block;
      width: 100%;
    }
    .reject {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ReviewLeaveDialog {
  private readonly leaveService = inject(LeaveService);
  private readonly dialogRef = inject(MatDialogRef<ReviewLeaveDialog, LeaveRequest>);
  private readonly data = inject<ReviewLeaveData>(MAT_DIALOG_DATA);

  protected readonly request = this.data.request;
  protected readonly isReject = this.data.decision === 'reject';
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly comment = new FormControl('', {
    nonNullable: true,
    validators: this.isReject ? [Validators.required, Validators.maxLength(500)] : [Validators.maxLength(500)],
  });

  protected save(): void {
    if (this.comment.invalid) {
      this.comment.markAsTouched();
      return;
    }

    const comment = this.comment.value.trim();
    const review$ = this.isReject
      ? this.leaveService.reject(this.request.id, comment)
      : this.leaveService.approve(this.request.id, comment || null);

    this.saving.set(true);
    this.errorMessage.set(null);

    review$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (updated) => this.dialogRef.close(updated),
      error: (error) => this.errorMessage.set(formErrorMessage(error)),
    });
  }
}
