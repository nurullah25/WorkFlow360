import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';

import { StatusBadge } from '../../shared/status-badge';
import { AttendanceDay, attendanceStatusLabel, attendanceStatusTone, formatMinutes } from './attendance.models';

@Component({
  selector: 'app-attendance-days-table',
  imports: [DatePipe, MatTableModule, StatusBadge],
  template: `
    <div class="table-scroll">
      <table mat-table [dataSource]="days()">
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let day" class="cell-primary">{{ day.date | date: 'EEE, d MMM' }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let day">
            @if (day.status !== 'Upcoming') {
              <app-status-badge [label]="statusLabel(day.status)" [tone]="statusTone(day.status)" />
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="checkIn">
          <th mat-header-cell *matHeaderCellDef>Check-in</th>
          <td mat-cell *matCellDef="let day">{{ day.checkInAt ? (day.checkInAt | date: 'HH:mm') : '' }}</td>
        </ng-container>
        <ng-container matColumnDef="checkOut">
          <th mat-header-cell *matHeaderCellDef>Check-out</th>
          <td mat-cell *matCellDef="let day">{{ day.checkOutAt ? (day.checkOutAt | date: 'HH:mm') : '' }}</td>
        </ng-container>
        <ng-container matColumnDef="worked">
          <th mat-header-cell *matHeaderCellDef>Worked</th>
          <td mat-cell *matCellDef="let day">{{ day.workedMinutes !== null ? formatMinutes(day.workedMinutes) : '' }}</td>
        </ng-container>
        <ng-container matColumnDef="note">
          <th mat-header-cell *matHeaderCellDef>Note</th>
          <td mat-cell *matCellDef="let day" class="cell-secondary">{{ day.note }}</td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let day; columns: columns" [class.non-working]="!day.isWorkingDay"></tr>
      </table>
    </div>
  `,
  styles: `
    tr.non-working {
      background: #f8f9fb;
      color: var(--wf-text-muted);
    }
  `,
})
export class AttendanceDaysTable {
  readonly days = input.required<AttendanceDay[]>();

  protected readonly columns = ['date', 'status', 'checkIn', 'checkOut', 'worked', 'note'];
  protected readonly statusLabel = attendanceStatusLabel;
  protected readonly statusTone = attendanceStatusTone;
  protected readonly formatMinutes = formatMinutes;
}
