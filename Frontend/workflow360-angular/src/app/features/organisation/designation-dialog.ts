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
import { Designation, OrganisationService } from './organisation.service';

@Component({
  selector: 'app-designation-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ designation ? 'Edit designation' : 'Add designation' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        <mat-form-field class="title-field">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="100" />
          <mat-error>{{ fieldError(form.controls.title) }}</mat-error>
        </mat-form-field>
        @if (designation) {
          <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .title-field {
      display: block;
      width: 100%;
      padding-top: 4px;
    }
  `,
})
export class DesignationDialog {
  private readonly organisationService = inject(OrganisationService);
  private readonly dialogRef = inject(MatDialogRef<DesignationDialog, Designation>);
  protected readonly designation = inject<Designation | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: [this.designation?.title ?? '', [Validators.required, Validators.maxLength(100)]],
    isActive: [this.designation?.isActive ?? true],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = this.form.getRawValue();
    const save$ = this.designation
      ? this.organisationService.updateDesignation(this.designation.id, request)
      : this.organisationService.createDesignation(request);

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
