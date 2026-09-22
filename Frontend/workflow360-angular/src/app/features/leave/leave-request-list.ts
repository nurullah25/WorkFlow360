import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, filter, merge, of, Subject, switchMap, tap } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { ConfirmService } from '../../shared/confirm-dialog';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { LEAVE_STATUSES, LeaveRequest, LeaveScope, LeaveStatus, leaveStatusTone } from './leave.models';
import { LeaveService } from './leave.service';
import { ReviewLeaveDialog, ReviewLeaveData } from './review-leave-dialog';

const COLUMNS: Record<LeaveScope, string[]> = {
  mine: ['leave', 'dates', 'days', 'status', 'review', 'actions'],
  approvals: ['employee', 'leave', 'dates', 'days', 'reason', 'actions'],
  team: ['employee', 'leave', 'dates', 'days', 'status', 'review'],
};

/** One list for "my requests", "waiting for my approval" and "team history"; the scope decides columns and filters. */
@Component({
  selector: 'app-leave-request-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    StatusBadge,
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
  templateUrl: './leave-request-list.html',
  styles: `
    .reason {
      max-width: 260px;
      white-space: normal;

      .cell-secondary {
        white-space: normal;
      }
    }
    .row-actions {
      display: flex;
      justify-content: flex-end;
      gap: 4px;
    }
    .reject-button {
      --mat-button-text-label-text-color: var(--mat-sys-error);
    }
  `,
})
export class LeaveRequestList {
  private readonly leaveService = inject(LeaveService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly scope = input.required<LeaveScope>();
  /** Bump this from the parent to reload, e.g. after a new request is submitted. */
  readonly reloadKey = input(0);
  readonly emptyText = input('Nothing to show.');
  /** Emitted after this list changed a request, so the parent can refresh balances and counts. */
  readonly changed = output<void>();
  readonly totalChange = output<number>();

  protected readonly columns = computed(() => COLUMNS[this.scope()]);
  protected readonly showFilters = computed(() => this.scope() === 'team');
  protected readonly statuses = LEAVE_STATUSES;
  protected readonly statusTone = leaveStatusTone;

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    status: '' as LeaveStatus | '',
  });

  protected readonly result = signal<PagedResult<LeaveRequest>>(emptyPage());
  protected readonly loading = signal(false);
  protected pageIndex = 0;
  protected pageSize = 10;

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() => {
          const { search, status } = this.filters.getRawValue();
          return this.leaveService
            .requests({ page: this.pageIndex + 1, pageSize: this.pageSize, scope: this.scope(), search, status })
            .pipe(catchError(() => of(emptyPage<LeaveRequest>())));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
        this.totalChange.emit(result.totalCount);
      });

    merge(toObservable(this.reloadKey), this.filters.valueChanges.pipe(debounceTime(300), tap(() => (this.pageIndex = 0))))
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.reload$.next());
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.reload$.next();
  }

  protected review(request: LeaveRequest, decision: 'approve' | 'reject'): void {
    const data: ReviewLeaveData = { request, decision };
    this.dialog
      .open(ReviewLeaveDialog, { data, width: '520px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((updated?: LeaveRequest) => {
        if (updated) {
          this.toast.success(`Leave for ${updated.employeeName} ${updated.status.toLowerCase()}.`);
          this.afterChange();
        }
      });
  }

  protected cancel(request: LeaveRequest): void {
    const wasApproved = request.status === 'Approved';
    this.confirmService
      .confirm({
        title: 'Cancel this leave?',
        message: wasApproved
          ? `Your approved ${request.leaveTypeName} will be cancelled and ${request.totalDays} day(s) returned to your balance.`
          : `Your pending ${request.leaveTypeName} request will be withdrawn.`,
        confirmText: 'Cancel leave',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.leaveService.cancel(request.id)),
      )
      .subscribe({
        next: () => {
          this.toast.success('Leave cancelled.');
          this.afterChange();
        },
        error: (error) => {
          const message = formErrorMessage(error);
          if (message) {
            this.toast.error(message);
          }
        },
      });
  }

  private afterChange(): void {
    this.reload$.next();
    this.changed.emit();
  }
}
