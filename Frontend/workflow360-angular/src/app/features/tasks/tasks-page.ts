import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import { catchError, debounceTime, of, Subject, switchMap, tap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { countLabel } from '../../shared/text';
import { TaskTable } from './task-table';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  TaskListItem,
  TaskPriority,
  TaskQuery,
  TaskStatus,
  taskStatusLabel,
} from './task.models';
import { TaskService } from './task.service';

type StatusFilter = 'open' | 'overdue' | 'all' | TaskStatus;

@Component({
  selector: 'app-tasks-page',
  imports: [
    ReactiveFormsModule,
    PageHeader,
    TaskTable,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
  ],
  template: `
    <app-page-header heading="Tasks" [subtitle]="countLabel(result().totalCount, 'task')" />

    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <form class="filter-bar" [formGroup]="filters">
        <mat-form-field class="search-field" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput formControlName="search" placeholder="Search task titles" />
        </mat-form-field>

        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Show</mat-label>
          <mat-select formControlName="scope">
            <mat-option value="mine">Assigned to me</mat-option>
            <mat-option value="all">All my projects</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select formControlName="status">
            <mat-option value="open">Open</mat-option>
            <mat-option value="overdue">Overdue</mat-option>
            @for (status of statuses; track status) {
              <mat-option [value]="status">{{ statusLabel(status) }}</mat-option>
            }
            <mat-option value="all">All</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            <mat-option [value]="null">Any priority</mat-option>
            @for (priority of priorities; track priority) {
              <mat-option [value]="priority">{{ priority }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>

      <app-task-table [tasks]="result().items" [loading]="loading()" (sortChange)="onSort($event)" />

      <mat-paginator
        [length]="result().totalCount"
        [pageIndex]="pageIndex"
        [pageSize]="pageSize"
        [pageSizeOptions]="[10, 20, 50]"
        (page)="onPage($event)"
      />
    </div>
  `,
})
export class TasksPage {
  private readonly taskService = inject(TaskService);

  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly statusLabel = taskStatusLabel;
  protected readonly countLabel = countLabel;

  // Employees mostly care about their own work; managers and admins about the whole project.
  private readonly defaultScope = inject(AuthService).hasRole('Employee') ? 'mine' : 'all';

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    scope: this.defaultScope as 'mine' | 'all',
    status: 'open' as StatusFilter,
    priority: null as TaskPriority | null,
  });

  protected readonly result = signal<PagedResult<TaskListItem>>(emptyPage());
  protected readonly loading = signal(false);

  private sort: Sort = { active: 'dueDate', direction: 'asc' };
  protected pageIndex = 0;
  protected pageSize = 20;

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() => this.taskService.list(this.buildQuery()).pipe(catchError(() => of(emptyPage<TaskListItem>())))),
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

  private buildQuery(): TaskQuery {
    const { search, scope, status, priority } = this.filters.getRawValue();
    const isSpecificStatus = status !== 'open' && status !== 'overdue' && status !== 'all';

    return {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      search,
      assignedToMe: scope === 'mine',
      status: isSpecificStatus ? status : null,
      openOnly: status === 'open',
      overdueOnly: status === 'overdue',
      priority,
      sortBy: this.sort.active,
      sortDirection: this.sort.direction,
    };
  }
}
