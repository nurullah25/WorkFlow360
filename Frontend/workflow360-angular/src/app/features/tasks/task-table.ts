import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';

import { StatusBadge } from '../../shared/status-badge';
import { TaskListItem, taskPriorityTone, taskStatusLabel, taskStatusTone } from './task.models';

/** Task list used on "Tasks" and on the project page. Paging and filtering stay with the parent. */
@Component({
  selector: 'app-task-table',
  imports: [DatePipe, MatTableModule, MatSortModule, StatusBadge],
  template: `
    <div class="table-scroll">
      <table
        mat-table
        [dataSource]="tasks()"
        matSort
        matSortActive="dueDate"
        matSortDirection="asc"
        matSortDisableClear
        (matSortChange)="sortChange.emit($event)"
      >
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="title">Task</th>
          <td mat-cell *matCellDef="let row" class="cell-primary title-cell">{{ row.title }}</td>
        </ng-container>

        <ng-container matColumnDef="project">
          <th mat-header-cell *matHeaderCellDef>Project</th>
          <td mat-cell *matCellDef="let row">{{ row.projectCode }}</td>
        </ng-container>

        <ng-container matColumnDef="assignee">
          <th mat-header-cell *matHeaderCellDef>Assignee</th>
          <td mat-cell *matCellDef="let row">{{ row.assigneeName ?? 'Unassigned' }}</td>
        </ng-container>

        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="priority">Priority</th>
          <td mat-cell *matCellDef="let row">
            <app-status-badge [label]="row.priority" [tone]="priorityTone(row.priority)" />
          </td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="status">Status</th>
          <td mat-cell *matCellDef="let row">
            <app-status-badge [label]="statusLabel(row.status)" [tone]="statusTone(row.status)" />
          </td>
        </ng-container>

        <ng-container matColumnDef="dueDate">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="dueDate">Due</th>
          <td mat-cell *matCellDef="let row" [class.overdue]="row.isOverdue">
            {{ row.dueDate ? (row.dueDate | date: 'mediumDate') : '—' }}
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns()"></tr>
        <tr mat-row *matRowDef="let row; columns: columns()" class="clickable-row" (click)="open(row)"></tr>
        <tr class="mat-mdc-row empty-row" *matNoDataRow>
          <td [attr.colspan]="columns().length">{{ loading() ? 'Loading…' : emptyText() }}</td>
        </tr>
      </table>
    </div>
  `,
  styles: `
    .title-cell {
      min-width: 240px;
    }
    .overdue {
      color: #a4262c;
      font-weight: 500;
    }
  `,
})
export class TaskTable {
  private readonly router = inject(Router);

  readonly tasks = input.required<TaskListItem[]>();
  readonly showProject = input(true);
  readonly loading = input(false);
  readonly emptyText = input('No tasks match these filters.');
  readonly sortChange = output<Sort>();

  protected readonly columns = computed(() =>
    this.showProject()
      ? ['title', 'project', 'assignee', 'priority', 'status', 'dueDate']
      : ['title', 'assignee', 'priority', 'status', 'dueDate'],
  );

  protected readonly statusLabel = taskStatusLabel;
  protected readonly statusTone = taskStatusTone;
  protected readonly priorityTone = taskPriorityTone;

  protected open(task: TaskListItem): void {
    this.router.navigate(['/tasks', task.id]);
  }
}
