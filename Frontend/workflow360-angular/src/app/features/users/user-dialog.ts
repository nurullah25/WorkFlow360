import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, filter, finalize, startWith, switchMap, tap } from 'rxjs';

import { Role } from '../../core/auth/auth.models';
import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { fieldError } from '../../shared/form-errors';
import { EmployeeLookup } from '../employees/employee.models';
import { EmployeeService } from '../employees/employee.service';
import { ROLES, UserListItem, UserService } from './user.service';

/** Mirrors the API password rules so most mistakes are caught before submitting. */
export const PASSWORD_VALIDATORS = [
  Validators.required,
  Validators.minLength(8),
  Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
];

@Component({
  selector: 'app-user-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatButtonModule,
  ],
  templateUrl: './user-dialog.html',
})
export class UserDialog {
  private readonly userService = inject(UserService);
  private readonly employeeService = inject(EmployeeService);
  private readonly dialogRef = inject(MatDialogRef<UserDialog, UserListItem>);
  protected readonly user = inject<UserListItem | null>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly roles = ROLES;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: [this.user?.email ?? '', [Validators.required, Validators.email]],
    fullName: [this.user?.fullName ?? '', [Validators.required, Validators.maxLength(100)]],
    role: [this.user?.role ?? ('Employee' as Role)],
    password: ['', this.user ? [] : PASSWORD_VALIDATORS],
    employeeId: [null as number | null],
  });

  protected readonly employeeSearch = new FormControl<string | EmployeeLookup>('', { nonNullable: true });

  protected readonly employeeOptions = toSignal(
    this.employeeSearch.valueChanges.pipe(
      filter((value): value is string => typeof value === 'string'),
      tap(() => this.form.controls.employeeId.setValue(null)),
      startWith(''),
      debounceTime(250),
      switchMap((term) => this.employeeService.lookup(term, true)),
    ),
    { initialValue: [] },
  );

  protected displayEmployee(value: string | EmployeeLookup | null): string {
    if (!value || typeof value === 'string') {
      return value ?? '';
    }
    return `${value.fullName} · ${value.employeeCode}`;
  }

  protected onEmployeeSelected(event: MatAutocompleteSelectedEvent): void {
    const employee = event.option.value as EmployeeLookup;
    this.form.patchValue({ employeeId: employee.id, fullName: employee.fullName, email: employee.email });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const save$ = this.user
      ? this.userService.update(this.user.id, {
          fullName: value.fullName,
          role: value.role,
          isActive: this.user.isActive,
        })
      : this.userService.create(value);

    this.saving.set(true);
    this.errorMessage.set(null);

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
