import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, filter, of, startWith, Subject, switchMap, tap } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { ConfirmService } from '../../shared/confirm-dialog';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { AllocationDialog } from './allocation-dialog';
import { HolidayDialog } from './holiday-dialog';
import { EmployeeLeaveBalance, Holiday, LeaveType } from './leave.models';
import { LeaveService } from './leave.service';
import { LeaveTypeDialog } from './leave-type-dialog';

@Component({
  selector: 'app-leave-settings-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    PageHeader,
    StatusBadge,
    MatTabsModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
  ],
  templateUrl: './leave-settings-page.html',
  styles: `
    .tab-toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 16px;
    }
    .tab-toolbar mat-form-field {
      width: 140px;
    }
  `,
})
export class LeaveSettingsPage {
  private readonly leaveService = inject(LeaveService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  private readonly currentYear = new Date().getFullYear();
  protected readonly years = [this.currentYear - 1, this.currentYear, this.currentYear + 1];

  // ----- Leave types -----
  protected readonly leaveTypes = signal<LeaveType[]>([]);
  protected readonly leaveTypeColumns = ['code', 'name', 'days', 'paid', 'status', 'actions'];

  // ----- Holidays -----
  protected readonly holidayYear = new FormControl(this.currentYear, { nonNullable: true });
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly holidayColumns = ['date', 'day', 'name', 'actions'];

  // ----- Balances -----
  protected readonly balanceFilters = inject(FormBuilder).nonNullable.group({
    year: this.currentYear,
    leaveTypeId: null as number | null,
    search: '',
  });
  protected readonly balances = signal<PagedResult<EmployeeLeaveBalance>>(emptyPage());
  protected readonly loadingBalances = signal(false);
  protected readonly balanceColumns = ['employee', 'type', 'allocated', 'used', 'pending', 'available', 'actions'];
  protected balancePageIndex = 0;
  protected balancePageSize = 20;
  private readonly reloadBalances$ = new Subject<void>();

  constructor() {
    this.loadLeaveTypes();

    this.holidayYear.valueChanges
      .pipe(startWith(this.currentYear), takeUntilDestroyed())
      .subscribe(() => this.loadHolidays());

    this.reloadBalances$
      .pipe(
        tap(() => this.loadingBalances.set(true)),
        switchMap(() => {
          const { year, leaveTypeId, search } = this.balanceFilters.getRawValue();
          return this.leaveService
            .balances({ page: this.balancePageIndex + 1, pageSize: this.balancePageSize, year, leaveTypeId, search })
            .pipe(catchError(() => of(emptyPage<EmployeeLeaveBalance>())));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.balances.set(result);
        this.loadingBalances.set(false);
      });

    this.balanceFilters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed()).subscribe(() => {
      this.balancePageIndex = 0;
      this.reloadBalances$.next();
    });
    this.reloadBalances$.next();
  }

  protected editLeaveType(leaveType: LeaveType | null): void {
    this.dialog
      .open(LeaveTypeDialog, { data: leaveType, width: '520px' })
      .afterClosed()
      .subscribe((saved?: LeaveType) => {
        if (saved) {
          this.toast.success(`${saved.name} saved.`);
          this.loadLeaveTypes();
          this.reloadBalances$.next();
        }
      });
  }

  protected addHoliday(): void {
    this.dialog
      .open(HolidayDialog, { width: '440px' })
      .afterClosed()
      .subscribe((holiday?: Holiday) => {
        if (holiday) {
          this.toast.success(`${holiday.name} added.`);
          this.loadHolidays();
        }
      });
  }

  protected deleteHoliday(holiday: Holiday): void {
    this.confirmService
      .confirm({
        title: 'Remove holiday?',
        message: `${holiday.name} will become a working day for new leave requests. Leave already approved keeps its day count.`,
        confirmText: 'Remove',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.leaveService.deleteHoliday(holiday.id)),
      )
      .subscribe(() => {
        this.toast.success(`${holiday.name} removed.`);
        this.loadHolidays();
      });
  }

  protected editAllocation(balance: EmployeeLeaveBalance): void {
    this.dialog
      .open(AllocationDialog, { data: balance, width: '440px' })
      .afterClosed()
      .subscribe((saved?: boolean) => {
        if (saved) {
          this.toast.success(`Allocation updated for ${balance.employeeName}.`);
          this.reloadBalances$.next();
        }
      });
  }

  protected generateBalances(): void {
    const year = this.balanceFilters.controls.year.value;
    this.confirmService
      .confirm({
        title: `Set up ${year} balances?`,
        message: `Every current employee gets the default allocation for each active leave type in ${year}. Balances that already exist are not changed.`,
        confirmText: 'Generate',
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.leaveService.generateBalances(year)),
      )
      .subscribe({
        next: (result) => {
          this.toast.success(
            result.created === 0 ? `All ${year} balances already exist.` : `Created ${result.created} balance(s) for ${year}.`,
          );
          this.reloadBalances$.next();
        },
        error: (error) => {
          const message = formErrorMessage(error);
          if (message) {
            this.toast.error(message);
          }
        },
      });
  }

  protected onBalancePage(event: PageEvent): void {
    this.balancePageIndex = event.pageIndex;
    this.balancePageSize = event.pageSize;
    this.reloadBalances$.next();
  }

  private loadLeaveTypes(): void {
    this.leaveService.leaveTypes(true).subscribe((types) => this.leaveTypes.set(types));
  }

  private loadHolidays(): void {
    this.leaveService.holidays(this.holidayYear.value).subscribe((holidays) => this.holidays.set(holidays));
  }
}
