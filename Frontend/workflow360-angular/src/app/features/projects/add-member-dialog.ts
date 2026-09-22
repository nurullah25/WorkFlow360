import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, filter, finalize, map, startWith, switchMap } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { EmployeeLookup } from '../employees/employee.models';
import { EmployeeService } from '../employees/employee.service';
import { ProjectDetails } from './project.models';
import { ProjectService } from './project.service';

@Component({
  selector: 'app-add-member-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Add member</h2>
    <mat-dialog-content>
      @if (errorMessage()) {
        <div class="form-error-banner">{{ errorMessage() }}</div>
      }
      <mat-form-field class="field">
        <mat-label>Employee</mat-label>
        <input matInput [formControl]="employeeSearch" [matAutocomplete]="employeeAuto" placeholder="Search by name or code" />
        <mat-autocomplete #employeeAuto="matAutocomplete" [displayWith]="displayEmployee">
          @for (option of options(); track option.id) {
            <mat-option [value]="option">
              {{ option.fullName }} <span class="cell-secondary">· {{ option.employeeCode }}</span>
            </mat-option>
          }
        </mat-autocomplete>
      </mat-form-field>
      <mat-form-field class="field">
        <mat-label>Role in project</mat-label>
        <input matInput [formControl]="roleControl" maxlength="50" placeholder="e.g. Developer, QA" />
        <mat-hint>Optional</mat-hint>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [disabled]="!selected() || saving()" (click)="save()">Add</button>
    </mat-dialog-actions>
  `,
  styles: `
    .field {
      display: block;
      width: 100%;
      padding-top: 4px;
    }
  `,
})
export class AddMemberDialog {
  private readonly projectService = inject(ProjectService);
  private readonly employeeService = inject(EmployeeService);
  private readonly dialogRef = inject(MatDialogRef<AddMemberDialog, ProjectDetails>);
  private readonly project = inject<ProjectDetails>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly employeeSearch = new FormControl<string | EmployeeLookup>('', { nonNullable: true });
  protected readonly roleControl = new FormControl('', { nonNullable: true, validators: Validators.maxLength(50) });

  private readonly existingIds = new Set([this.project.managerId, ...this.project.members.map((m) => m.employeeId)]);

  protected readonly selected = toSignal(
    this.employeeSearch.valueChanges.pipe(map((value) => (typeof value === 'string' ? null : value))),
    { initialValue: null },
  );

  protected readonly options = toSignal(
    this.employeeSearch.valueChanges.pipe(
      startWith(''),
      filter((value): value is string => typeof value === 'string'),
      debounceTime(250),
      switchMap((term) => this.employeeService.lookup(term)),
      map((employees) => employees.filter((e) => !this.existingIds.has(e.id))),
    ),
    { initialValue: [] },
  );

  protected displayEmployee(value: string | EmployeeLookup | null): string {
    return !value || typeof value === 'string' ? (value ?? '') : `${value.fullName} · ${value.employeeCode}`;
  }

  protected save(): void {
    const employee = this.selected();
    if (!employee) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.projectService
      .addMember(this.project.id, employee.id, this.roleControl.value || null)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (project) => this.dialogRef.close(project),
        error: (error) => this.errorMessage.set(formErrorMessage(error)),
      });
  }
}
