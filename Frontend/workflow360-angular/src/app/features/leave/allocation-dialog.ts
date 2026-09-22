import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { EmployeeLeaveBalance } from './leave.models';
import { LeaveService } from './leave.service';

@Component({
  selector: 'app-allocation-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Adjust allocation</h2>
    <mat-dialog-content>
      <p class="intro">
        {{ balance.leaveTypeName }} {{ balance.year }} for <strong>{{ balance.employeeName }}</strong>.
        {{ balance.usedDays }} day(s) used, {{ balance.pendingDays }} pending.
      </p>
      @if (errorMessage()) {
        <div class="form-error-banner">{{ errorMessage() }}</div>
      }
      <mat-form-field class="field">
        <mat-label>Allocated days</mat-label>
        <input matInput type="number" [formControl]="allocated" min="0" max="365" step="0.5" />
        <mat-error>Enter a number between 0 and 365.</mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [disabled]="saving()" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
  styles: `
    .intro {
      margin-top: 0;
    }
    .field {
      display: block;
      width: 100%;
    }
  `,
})
export class AllocationDialog {
  private readonly leaveService = inject(LeaveService);
  private readonly dialogRef = inject(MatDialogRef<AllocationDialog, boolean>);
  protected readonly balance = inject<EmployeeLeaveBalance>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly allocated = new FormControl(this.balance.allocatedDays, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(0), Validators.max(365)],
  });

  protected save(): void {
    if (this.allocated.invalid) {
      this.allocated.markAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.leaveService
      .updateAllocation(this.balance.id, this.allocated.value)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (error) => this.errorMessage.set(formErrorMessage(error)),
      });
  }
}
