import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../shared/page-header';
import { StatTile } from '../../shared/stat-tile';
import { StatusBadge } from '../../shared/status-badge';
import { leaveStatusTone } from '../leave/leave.models';
import { taskPriorityTone } from '../tasks/task.models';
import { Dashboard } from './dashboard.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [DatePipe, RouterLink, PageHeader, StatTile, StatusBadge, MatIconModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly auth = inject(AuthService);

  protected readonly dashboard = toSignal(
    inject(HttpClient).get<Dashboard>('/api/dashboard').pipe(catchError(() => of(null))),
  );

  protected readonly now = new Date();
  protected readonly leaveStatusTone = leaveStatusTone;
  protected readonly priorityTone = taskPriorityTone;

  protected readonly greeting = computed(() => {
    const hour = this.now.getHours();
    const partOfDay = hour < 12 ? 'morning' : hour < 18 ? 'afternoon' : 'evening';
    const firstName = this.auth.user()?.fullName.split(' ')[0] ?? '';
    return `Good ${partOfDay}, ${firstName}`;
  });

  /** The attendance tile reads differently depending on where the person is in their day. */
  protected readonly attendanceTile = computed(() => {
    const me = this.dashboard()?.me;
    if (!me) {
      return null;
    }
    if (me.onLeaveToday) {
      return { value: 'On leave', hint: me.onLeaveToday, tone: 'default' as const };
    }
    if (me.checkInAt) {
      const time = new Date(me.checkInAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
      return {
        value: time,
        hint: me.checkOutAt ? 'Checked out' : me.isLate ? 'Checked in late' : 'Checked in',
        tone: me.isLate ? ('warning' as const) : ('default' as const),
      };
    }
    return me.isWorkingDay
      ? { value: 'Not yet', hint: 'Check in on the Attendance page', tone: 'default' as const }
      : { value: 'Day off', hint: 'Weekend or public holiday', tone: 'default' as const };
  });

  protected readonly mainBalance = computed(() => {
    const balances = this.dashboard()?.me?.leaveBalances ?? [];
    return balances.find((b) => b.leaveTypeCode === 'AL') ?? balances[0] ?? null;
  });

  protected readonly maxHeadcount = computed(() =>
    Math.max(1, ...(this.dashboard()?.company?.headcount.map((d) => d.employees) ?? [])),
  );

  protected percent(done: number, total: number): number {
    return total === 0 ? 0 : Math.round((done / total) * 100);
  }
}
