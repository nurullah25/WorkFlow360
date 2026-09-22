import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, filter, finalize, map, of, switchMap, tap } from 'rxjs';

import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { ConfirmService } from '../../shared/confirm-dialog';
import { parseIsoDate, toIsoDate } from '../../shared/dates';
import { fieldError } from '../../shared/form-errors';
import { OrganisationService } from '../organisation/organisation.service';
import {
  EMPLOYMENT_STATUSES,
  EmployeeDetails,
  EmployeeLookup,
  EmploymentStatus,
  isFormerStatus,
  SaveEmployee,
} from './employee.models';
import { EmployeeService } from './employee.service';

@Component({
  selector: 'app-employee-dialog',
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
  templateUrl: './employee-dialog.html',
})
export class EmployeeDialog {
  private readonly employeeService = inject(EmployeeService);
  private readonly organisationService = inject(OrganisationService);
  private readonly confirmService = inject(ConfirmService);
  private readonly dialogRef = inject(MatDialogRef<EmployeeDialog, EmployeeDetails>);
  protected readonly employee = inject<EmployeeDetails | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly statuses = EMPLOYMENT_STATUSES;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    employeeCode: [this.employee?.employeeCode ?? '', [Validators.required, Validators.maxLength(20)]],
    firstName: [this.employee?.firstName ?? '', [Validators.required, Validators.maxLength(50)]],
    lastName: [this.employee?.lastName ?? '', [Validators.required, Validators.maxLength(50)]],
    email: [this.employee?.email ?? '', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: [this.employee?.phone ?? '', Validators.maxLength(30)],
    departmentId: [this.employee?.departmentId ?? (null as number | null), Validators.required],
    designationId: [this.employee?.designationId ?? (null as number | null), Validators.required],
    managerId: [this.employee?.managerId ?? (null as number | null)],
    joiningDate: [
      this.employee ? parseIsoDate(this.employee.joiningDate) : (null as Date | null),
      Validators.required,
    ],
    status: [this.employee?.status ?? ('Probation' as EmploymentStatus)],
  });

  // Inactive departments/designations are hidden unless the employee is still assigned to one.
  private readonly allDepartments = toSignal(this.organisationService.departments(true), { initialValue: [] });
  private readonly allDesignations = toSignal(this.organisationService.designations(true), { initialValue: [] });
  protected readonly departments = computed(() =>
    this.allDepartments().filter((d) => d.isActive || d.id === this.employee?.departmentId),
  );
  protected readonly designations = computed(() =>
    this.allDesignations().filter((d) => d.isActive || d.id === this.employee?.designationId),
  );

  protected readonly managerSearch = new FormControl<string | EmployeeLookup>(
    this.employee?.managerId
      ? { id: this.employee.managerId, fullName: this.employee.managerName ?? '', employeeCode: '', email: '' }
      : '',
    { nonNullable: true },
  );

  protected readonly managerOptions = toSignal(
    this.managerSearch.valueChanges.pipe(
      filter((value): value is string => typeof value === 'string'),
      // Typing replaces any previous selection until an option is picked again.
      tap(() => this.form.controls.managerId.setValue(null)),
      debounceTime(250),
      switchMap((term) => (term.trim() ? this.employeeService.lookup(term) : of([]))),
      map((options) => options.filter((option) => option.id !== this.employee?.id)),
    ),
    { initialValue: [] },
  );

  private readonly status = toSignal(this.form.controls.status.valueChanges, {
    initialValue: this.form.controls.status.value,
  });

  protected readonly endsEmployment = computed(
    () => !!this.employee && !isFormerStatus(this.employee.status) && isFormerStatus(this.status()),
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
    const typedManager = this.managerSearch.value;
    if (typeof typedManager === 'string' && typedManager.trim() !== '') {
      this.managerSearch.setErrors({ server: 'Pick a manager from the list, or clear the field.' });
      this.managerSearch.markAsTouched();
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.endsEmployment()) {
      this.submit();
      return;
    }

    const name = `${this.employee!.firstName} ${this.employee!.lastName}`;
    this.confirmService
      .confirm({
        title: 'End employment?',
        message: `${name} will be marked as ${this.status()}. Their user account will be deactivated and they will be signed out.`,
        confirmText: 'End employment',
        destructive: true,
      })
      .subscribe((confirmed) => {
        if (confirmed) {
          this.submit();
        }
      });
  }

  private submit(): void {
    const value = this.form.getRawValue();
    const request: SaveEmployee = {
      employeeCode: value.employeeCode,
      firstName: value.firstName,
      lastName: value.lastName,
      email: value.email,
      phone: value.phone || null,
      departmentId: value.departmentId!,
      designationId: value.designationId!,
      managerId: value.managerId,
      joiningDate: toIsoDate(value.joiningDate!),
      status: value.status,
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const save$ = this.employee
      ? this.employeeService.update(this.employee.id, request)
      : this.employeeService.create(request);

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
