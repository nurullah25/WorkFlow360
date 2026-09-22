import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, of, Subject, switchMap, tap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast.service';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { countLabel } from '../../shared/text';
import { OrganisationService } from '../organisation/organisation.service';
import { EmployeeDialog } from './employee-dialog';
import {
  EMPLOYMENT_STATUSES,
  EmployeeDetails,
  EmployeeListItem,
  EmployeeQuery,
  EmploymentStatus,
  employmentStatusTone,
} from './employee.models';
import { EmployeeService } from './employee.service';

/** '' = current employees only, 'all' = include people who have left. */
type StatusFilter = '' | 'all' | EmploymentStatus;

@Component({
  selector: 'app-employees-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    PageHeader,
    StatusBadge,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './employees-page.html',
})
export class EmployeesPage {
  private readonly employeeService = inject(EmployeeService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  protected readonly canManage = inject(AuthService).hasRole('Admin', 'HR');
  protected readonly countLabel = countLabel;
  protected readonly statuses = EMPLOYMENT_STATUSES;
  protected readonly statusTone = employmentStatusTone;
  protected readonly columns = ['employee', 'department', 'designation', 'manager', 'joiningDate', 'status'];

  protected readonly departments = toSignal(inject(OrganisationService).departments(), {
    initialValue: [],
  });

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    departmentId: null as number | null,
    status: '' as StatusFilter,
  });

  protected readonly result = signal<PagedResult<EmployeeListItem>>(emptyPage());
  protected readonly loading = signal(false);

  private sort: Sort = { active: 'name', direction: 'asc' };
  protected pageIndex = 0;
  protected pageSize = 20;

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() => this.employeeService.list(this.buildQuery()).pipe(catchError(() => of(emptyPage<EmployeeListItem>())))),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed()).subscribe(() => {
      this.pageIndex = 0;
      this.reload$.next();
    });

    this.reload$.next();
  }

  protected onSort(sort: Sort): void {
    this.sort = sort;
    this.pageIndex = 0;
    this.reload$.next();
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.reload$.next();
  }

  protected openCreate(): void {
    this.openDialog(null);
  }

  protected openEdit(row: EmployeeListItem): void {
    if (!this.canManage) {
      return;
    }
    this.employeeService.get(row.id).subscribe((employee) => this.openDialog(employee));
  }

  private openDialog(employee: EmployeeDetails | null): void {
    this.dialog
      .open(EmployeeDialog, { data: employee, width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: EmployeeDetails) => {
        if (saved) {
          this.toast.success(`${saved.firstName} ${saved.lastName} saved.`);
          this.reload$.next();
        }
      });
  }

  private buildQuery(): EmployeeQuery {
    const { search, departmentId, status } = this.filters.getRawValue();
    return {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      search,
      departmentId,
      status: status === '' || status === 'all' ? null : status,
      includeFormer: status === 'all',
      sortBy: this.sort.active,
      sortDirection: this.sort.direction,
    };
  }
}
