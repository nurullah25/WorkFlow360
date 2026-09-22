import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';

import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast.service';
import { PageHeader } from '../../shared/page-header';
import { LeaveRequestList } from './leave-request-list';
import { LeaveBalance, LeaveRequest } from './leave.models';
import { LeaveService } from './leave.service';
import { RequestLeaveDialog } from './request-leave-dialog';

type LeaveTab = 'mine' | 'approvals' | 'team';

@Component({
  selector: 'app-leave-page',
  imports: [PageHeader, LeaveRequestList, MatTabsModule, MatButtonModule, MatIconModule],
  templateUrl: './leave-page.html',
  styleUrl: './leave-page.scss',
})
export class LeavePage implements OnInit {
  private readonly leaveService = inject(LeaveService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  /** Bound from the ?tab= query parameter, e.g. links in "leave request to review" notifications. */
  readonly tab = input<LeaveTab>();

  protected readonly hasEmployeeRecord = this.auth.user()?.employeeId != null;
  protected readonly canReview = this.auth.hasRole('Admin', 'HR', 'Manager');

  private readonly tabs: LeaveTab[] = [
    ...(this.hasEmployeeRecord ? (['mine'] as const) : []),
    ...(this.canReview ? (['approvals', 'team'] as const) : []),
  ];

  protected readonly selectedIndex = computed(() => Math.max(0, this.tabs.indexOf(this.tab() ?? this.tabs[0])));

  protected readonly balances = signal<LeaveBalance[]>([]);
  protected readonly pendingApprovals = signal(0);
  protected readonly reloadKey = signal(0);

  ngOnInit(): void {
    this.loadBalances();
  }

  protected requestLeave(): void {
    this.dialog
      .open(RequestLeaveDialog, { width: '520px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((request?: LeaveRequest) => {
        if (request) {
          this.toast.success(`Leave requested: ${request.totalDays} day(s). Your manager has been notified.`);
          this.onChanged();
        }
      });
  }

  protected onChanged(): void {
    this.reloadKey.update((key) => key + 1);
    this.loadBalances();
  }

  private loadBalances(): void {
    if (this.hasEmployeeRecord) {
      this.leaveService.myBalances().subscribe((balances) => this.balances.set(balances));
    }
  }
}
