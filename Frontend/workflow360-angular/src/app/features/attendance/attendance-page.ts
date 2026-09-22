import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { catchError, debounceTime, finalize, merge, Observable, of, Subject, switchMap, tap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { AttendanceDaysTable } from './attendance-days-table';
import { AttendanceSummaryTiles } from './attendance-summary';
import { toIsoDate } from '../../shared/dates';
import {
  EmployeeMonth,
  formatMinutes,
  pastDaysNewestFirst,
  TeamAttendanceRow,
  TodayAttendance,
  YearMonth,
} from './attendance.models';
import { AttendanceService } from './attendance.service';
import { EmployeeMonthDialog, EmployeeMonthDialogData } from './employee-month-dialog';
import { MonthSwitcher } from './month-switcher';

function currentMonth(): YearMonth {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

@Component({
  selector: 'app-attendance-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    PageHeader,
    StatusBadge,
    MonthSwitcher,
    AttendanceSummaryTiles,
    AttendanceDaysTable,
    MatTabsModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './attendance-page.html',
  styleUrl: './attendance-page.scss',
})
export class AttendancePage {
  private readonly attendanceService = inject(AttendanceService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  protected readonly hasEmployeeRecord = this.auth.user()?.employeeId != null;
  protected readonly canSeeTeam = this.auth.hasRole('Admin', 'HR', 'Manager');
  protected readonly formatMinutes = formatMinutes;

  // ----- Today -----
  protected readonly today = signal<TodayAttendance | null>(null);
  protected readonly busy = signal(false);

  // ----- My month -----
  protected readonly myPeriod = signal<YearMonth>(currentMonth());
  private readonly myRefresh = signal(0);
  protected readonly myMonth = toSignal(
    toObservable(computed(() => ({ period: this.myPeriod(), refresh: this.myRefresh() }))).pipe(
      switchMap(({ period }) =>
        this.hasEmployeeRecord ? this.attendanceService.myMonth(period).pipe(catchError(() => of(null))) : of(null),
      ),
    ),
    { initialValue: null as EmployeeMonth | null },
  );

  protected readonly myDays = computed(() => pastDaysNewestFirst(this.myMonth()?.days ?? [], toIsoDate(new Date())));

  // ----- Team -----
  protected readonly teamPeriod = signal<YearMonth>(currentMonth());
  protected readonly teamSearch = new FormControl('', { nonNullable: true });
  protected readonly team = signal<PagedResult<TeamAttendanceRow>>(emptyPage());
  protected readonly loadingTeam = signal(false);
  protected readonly teamColumns = ['employee', 'department', 'present', 'late', 'onLeave', 'absent', 'worked'];
  protected teamPageIndex = 0;
  protected teamPageSize = 20;
  private readonly reloadTeam$ = new Subject<void>();

  constructor() {
    if (this.hasEmployeeRecord) {
      this.attendanceService.today().subscribe((today) => this.today.set(today));
    }

    if (this.canSeeTeam) {
      this.reloadTeam$
        .pipe(
          tap(() => this.loadingTeam.set(true)),
          switchMap(() =>
            this.attendanceService
              .team(this.teamPeriod(), this.teamPageIndex + 1, this.teamPageSize, this.teamSearch.value)
              .pipe(catchError(() => of(emptyPage<TeamAttendanceRow>()))),
          ),
          takeUntilDestroyed(),
        )
        .subscribe((team) => {
          this.team.set(team);
          this.loadingTeam.set(false);
        });

      merge(toObservable(this.teamPeriod), this.teamSearch.valueChanges.pipe(debounceTime(300)))
        .pipe(takeUntilDestroyed())
        .subscribe(() => {
          this.teamPageIndex = 0;
          this.reloadTeam$.next();
        });
    }
  }

  protected checkIn(): void {
    this.runTodayAction(this.attendanceService.checkIn(), (today) =>
      today.isLate ? 'Checked in. You are marked late today.' : 'Checked in. Have a good day!',
    );
  }

  protected checkOut(): void {
    this.runTodayAction(this.attendanceService.checkOut(), (today) => `Checked out after ${formatMinutes(today.workedMinutes)}.`);
  }

  protected openEmployee(row: TeamAttendanceRow): void {
    const data: EmployeeMonthDialogData = {
      employeeId: row.employeeId,
      employeeName: row.employeeName,
      period: this.teamPeriod(),
    };
    this.dialog.open(EmployeeMonthDialog, { data, width: '860px', maxWidth: '95vw' });
  }

  protected onTeamPage(event: PageEvent): void {
    this.teamPageIndex = event.pageIndex;
    this.teamPageSize = event.pageSize;
    this.reloadTeam$.next();
  }

  private runTodayAction(action$: Observable<TodayAttendance>, message: (today: TodayAttendance) => string): void {
    this.busy.set(true);
    action$.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: (today) => {
        this.today.set(today);
        this.myRefresh.update((value) => value + 1);
        this.toast.success(message(today));
      },
      error: (error) => {
        const text = formErrorMessage(error);
        if (text) {
          this.toast.error(text);
        }
      },
    });
  }
}
