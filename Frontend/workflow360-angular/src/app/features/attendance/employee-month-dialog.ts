import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, of, switchMap } from 'rxjs';

import { AttendanceDaysTable } from './attendance-days-table';
import { AttendanceSummaryTiles } from './attendance-summary';
import { toIsoDate } from '../../shared/dates';
import { EmployeeMonth, pastDaysNewestFirst, YearMonth } from './attendance.models';
import { AttendanceService } from './attendance.service';
import { MonthSwitcher } from './month-switcher';

export interface EmployeeMonthDialogData {
  employeeId: number;
  employeeName: string;
  period: YearMonth;
}

@Component({
  selector: 'app-employee-month-dialog',
  imports: [MatDialogModule, MatButtonModule, MatProgressBarModule, MonthSwitcher, AttendanceSummaryTiles, AttendanceDaysTable],
  template: `
    <h2 mat-dialog-title>{{ data.employeeName }}</h2>
    <mat-dialog-content>
      <div class="toolbar">
        <app-month-switcher [(period)]="period" />
      </div>
      @if (month(); as month) {
        <app-attendance-summary [summary]="month.summary" />
        <div class="days">
          <app-attendance-days-table [days]="pastAndToday()" />
        </div>
      } @else {
        <mat-progress-bar mode="indeterminate" />
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .toolbar {
      margin-bottom: 12px;
    }
    .days {
      margin-top: 16px;
      border: 1px solid var(--wf-border);
      border-radius: 8px;
    }
  `,
})
export class EmployeeMonthDialog {
  private readonly attendanceService = inject(AttendanceService);
  protected readonly data = inject<EmployeeMonthDialogData>(MAT_DIALOG_DATA);

  protected readonly period = signal<YearMonth>(this.data.period);

  protected readonly month = toSignal(
    toObservable(this.period).pipe(
      switchMap((period) =>
        this.attendanceService.employeeMonth(this.data.employeeId, period).pipe(catchError(() => of(null))),
      ),
    ),
    { initialValue: null as EmployeeMonth | null },
  );

  protected readonly pastAndToday = computed(() =>
    pastDaysNewestFirst(this.month()?.days ?? [], toIsoDate(new Date())),
  );
}
