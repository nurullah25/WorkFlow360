import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, of, Subject, switchMap, tap } from 'rxjs';

import { toIsoDate } from '../../shared/dates';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult, toHttpParams } from '../../shared/paging';

interface AuditLogEntry {
  id: number;
  createdAt: string;
  userId: number | null;
  userName: string | null;
  userEmail: string | null;
  action: string;
  entityType: string;
  entityId: number | null;
  details: string | null;
  ipAddress: string | null;
}

/** "LeaveApproved" -> "Leave approved" */
function humanize(action: string): string {
  const words = action.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

@Component({
  selector: 'app-audit-details-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ humanize(entry.action) }}</h2>
    <mat-dialog-content>
      <dl>
        <dt>When</dt>
        <dd>{{ entry.createdAt | date: 'medium' }}</dd>
        <dt>Who</dt>
        <dd>{{ entry.userName ?? 'Unknown / not signed in' }} {{ entry.userEmail ? '(' + entry.userEmail + ')' : '' }}</dd>
        <dt>Record</dt>
        <dd>{{ entry.entityType }}{{ entry.entityId ? ' #' + entry.entityId : '' }}</dd>
        <dt>IP address</dt>
        <dd>{{ entry.ipAddress ?? '—' }}</dd>
      </dl>
      <pre>{{ prettyDetails }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    dl {
      display: grid;
      grid-template-columns: 110px 1fr;
      gap: 6px 12px;
      margin: 0 0 12px;
    }
    dt {
      color: var(--wf-text-muted);
    }
    dd {
      margin: 0;
    }
    pre {
      margin: 0;
      padding: 12px;
      max-height: 320px;
      overflow: auto;
      border-radius: 6px;
      background: #f4f5f7;
      font-size: 12px;
    }
  `,
})
export class AuditDetailsDialog {
  protected readonly entry = inject<AuditLogEntry>(MAT_DIALOG_DATA);
  protected readonly humanize = humanize;
  protected readonly prettyDetails = this.formatDetails(this.entry.details);

  private formatDetails(details: string | null): string {
    if (!details) {
      return 'No additional details.';
    }
    try {
      return JSON.stringify(JSON.parse(details), null, 2);
    } catch {
      return details;
    }
  }
}

@Component({
  selector: 'app-audit-log-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    PageHeader,
    MatTableModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatIconModule,
  ],
  template: `
    <app-page-header heading="Audit log" subtitle="Sign-ins, changes to people and approvals. Newest first." />

    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <form class="filter-bar" [formGroup]="filters">
        <mat-form-field class="search-field" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput formControlName="search" placeholder="Search user name or email" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Action</mat-label>
          <mat-select formControlName="action">
            <mat-option value="">All actions</mat-option>
            @for (action of actions(); track action) {
              <mat-option [value]="action">{{ humanize(action) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic" class="dates">
          <mat-label>Date range</mat-label>
          <mat-date-range-input [rangePicker]="picker">
            <input matStartDate formControlName="from" placeholder="From" />
            <input matEndDate formControlName="to" placeholder="To" />
          </mat-date-range-input>
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-date-range-picker #picker />
        </mat-form-field>
      </form>

      <div class="table-scroll">
        <table mat-table [dataSource]="result().items">
          <ng-container matColumnDef="time">
            <th mat-header-cell *matHeaderCellDef>When</th>
            <td mat-cell *matCellDef="let row">{{ row.createdAt | date: 'd MMM y, HH:mm:ss' }}</td>
          </ng-container>
          <ng-container matColumnDef="user">
            <th mat-header-cell *matHeaderCellDef>User</th>
            <td mat-cell *matCellDef="let row">
              <div class="cell-primary">{{ row.userName ?? '—' }}</div>
              <div class="cell-secondary">{{ row.userEmail }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="action">
            <th mat-header-cell *matHeaderCellDef>Action</th>
            <td mat-cell *matCellDef="let row" [class.failed]="row.action === 'LoginFailed' || row.action === 'SessionsRevoked'">
              {{ humanize(row.action) }}
            </td>
          </ng-container>
          <ng-container matColumnDef="entity">
            <th mat-header-cell *matHeaderCellDef>Record</th>
            <td mat-cell *matCellDef="let row">{{ row.entityType }}{{ row.entityId ? ' #' + row.entityId : '' }}</td>
          </ng-container>
          <ng-container matColumnDef="ip">
            <th mat-header-cell *matHeaderCellDef>IP address</th>
            <td mat-cell *matCellDef="let row" class="cell-secondary">{{ row.ipAddress }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" class="clickable-row" (click)="openDetails(row)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No entries match these filters.' }}</td>
          </tr>
        </table>
      </div>

      <mat-paginator
        [length]="result().totalCount"
        [pageIndex]="pageIndex"
        [pageSize]="pageSize"
        [pageSizeOptions]="[25, 50, 100]"
        (page)="onPage($event)"
      />
    </div>
  `,
  styles: `
    .filter-bar .dates {
      width: 260px;
    }
    .failed {
      color: #a4262c;
      font-weight: 500;
    }
  `,
})
export class AuditLogPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);

  protected readonly humanize = humanize;
  protected readonly columns = ['time', 'user', 'action', 'entity', 'ip'];
  protected readonly actions = toSignal(this.http.get<string[]>('/api/audit-logs/actions'), { initialValue: [] });

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    action: '',
    from: null as Date | null,
    to: null as Date | null,
  });

  protected readonly result = signal<PagedResult<AuditLogEntry>>(emptyPage());
  protected readonly loading = signal(false);
  protected pageIndex = 0;
  protected pageSize = 25;
  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() => {
          const { search, action, from, to } = this.filters.getRawValue();
          const params = toHttpParams({
            page: this.pageIndex + 1,
            pageSize: this.pageSize,
            search,
            action,
            from: from ? toIsoDate(from) : null,
            to: to ? toIsoDate(to) : null,
          });
          return this.http
            .get<PagedResult<AuditLogEntry>>('/api/audit-logs', { params })
            .pipe(catchError(() => of(emptyPage<AuditLogEntry>())));
        }),
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

  protected onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.reload$.next();
  }

  protected openDetails(entry: AuditLogEntry): void {
    this.dialog.open(AuditDetailsDialog, { data: entry, width: '560px', maxWidth: '95vw' });
  }
}
