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
import { Department, OrganisationService } from './organisation.service';

@Component({
  selector: 'app-department-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ department ? 'Edit department' : 'Add department' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        <div class="form-grid">
          <mat-form-field>
            <mat-label>Code</mat-label>
            <input matInput formControlName="code" maxlength="20" />
            <mat-error>{{ fieldError(form.controls.code) }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" maxlength="100" />
            <mat-error>{{ fieldError(form.controls.name) }}</mat-error>
          </mat-form-field>
          @if (department) {
            <mat-slide-toggle class="full-width" formControlName="isActive">Active</mat-slide-toggle>
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
export class DepartmentDialog {
  private readonly organisationService = inject(OrganisationService);
  private readonly dialogRef = inject(MatDialogRef<DepartmentDialog, Department>);
  protected readonly department = inject<Department | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    code: [this.department?.code ?? '', [Validators.required, Validators.maxLength(20)]],
    name: [this.department?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    isActive: [this.department?.isActive ?? true],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = this.form.getRawValue();
    const save$ = this.department
      ? this.organisationService.updateDepartment(this.department.id, request)
      : this.organisationService.createDepartment(request);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error) => {
        if (!applyServerErrors(this.form, error)) {
          this.errorMessage.set(formErrorMessage(error));
        }
      },
    });
  }
}
