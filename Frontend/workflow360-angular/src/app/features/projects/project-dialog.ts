import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, filter, finalize, of, switchMap, tap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { parseIsoDate, toIsoDate } from '../../shared/dates';
import { fieldError } from '../../shared/form-errors';
import { EmployeeLookup } from '../employees/employee.models';
import { EmployeeService } from '../employees/employee.service';
import {
  PROJECT_STATUSES,
  ProjectDetails,
  ProjectStatus,
  projectStatusLabel,
  SaveProject,
} from './project.models';
import { ProjectService } from './project.service';

@Component({
  selector: 'app-project-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatAutocompleteModule,
    MatButtonModule,
  ],
  templateUrl: './project-dialog.html',
})
export class ProjectDialog {
  private readonly projectService = inject(ProjectService);
  private readonly employeeService = inject(EmployeeService);
  private readonly dialogRef = inject(MatDialogRef<ProjectDialog, ProjectDetails>);
  private readonly auth = inject(AuthService);
  protected readonly project = inject<ProjectDetails | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly statusLabel = projectStatusLabel;
  protected readonly statuses: ProjectStatus[] = this.project ? PROJECT_STATUSES : ['Planned', 'Active'];
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  // Only admins pick the project manager; a manager's own projects are always managed by them.
  protected readonly canChooseManager = this.auth.hasRole('Admin');

  protected readonly form = inject(FormBuilder).nonNullable.group({
    code: [this.project?.code ?? '', [Validators.required, Validators.maxLength(20)]],
    name: [this.project?.name ?? '', [Validators.required, Validators.maxLength(150)]],
    description: [this.project?.description ?? '', Validators.maxLength(2000)],
    managerId: [this.project?.managerId ?? this.auth.user()?.employeeId ?? (null as number | null), Validators.required],
    startDate: [this.project ? parseIsoDate(this.project.startDate) : (new Date() as Date | null), Validators.required],
    endDate: [this.project?.endDate ? parseIsoDate(this.project.endDate) : (null as Date | null)],
    status: [this.project?.status ?? ('Planned' as ProjectStatus)],
  });

  protected readonly managerSearch = new FormControl<string | EmployeeLookup>(
    this.project
      ? { id: this.project.managerId, fullName: this.project.managerName, employeeCode: '', email: '' }
      : '',
    { nonNullable: true },
  );

  protected readonly managerOptions = toSignal(
    this.managerSearch.valueChanges.pipe(
      filter((value): value is string => typeof value === 'string'),
      tap(() => this.form.controls.managerId.setValue(null)),
      debounceTime(250),
      switchMap((term) => (term.trim() ? this.employeeService.lookup(term) : of([]))),
    ),
    { initialValue: [] },
  );

  protected displayManager(value: string | EmployeeLookup | null): string {
    if (!value || typeof value === 'string') {
      return value ?? '';
    }
    return value.employeeCode ? `${value.fullName} · ${value.employeeCode}` : value.fullName;
  }

  protected onManagerSelected(event: MatAutocompleteSelectedEvent): void {
    this.form.controls.managerId.setValue((event.option.value as EmployeeLookup).id);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.form.controls.managerId.invalid && this.canChooseManager) {
        this.managerSearch.setErrors({ server: 'Pick a project manager from the list.' });
        this.managerSearch.markAsTouched();
      }
      return;
    }

    const value = this.form.getRawValue();
    const request: SaveProject = {
      code: value.code,
      name: value.name,
      description: value.description || null,
      managerId: value.managerId!,
      startDate: toIsoDate(value.startDate!),
      endDate: value.endDate ? toIsoDate(value.endDate) : null,
      status: value.status,
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const save$ = this.project
      ? this.projectService.update(this.project.id, request)
      : this.projectService.create(request);

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
