import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { catchError, filter, of, Subject, switchMap, tap } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { ConfirmService } from '../../shared/confirm-dialog';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { TaskDialog, TaskDialogData } from '../tasks/task-dialog';
import { TaskTable } from '../tasks/task-table';
import { TASK_STATUSES, TaskDetails, TaskListItem, TaskStatus, taskStatusLabel } from '../tasks/task.models';
import { TaskService } from '../tasks/task.service';
import { AddMemberDialog } from './add-member-dialog';
import { ProjectDialog } from './project-dialog';
import {
  isClosedProject,
  ProjectDetails,
  ProjectMember,
  projectStatusLabel,
  projectStatusTone,
} from './project.models';
import { ProjectService } from './project.service';

type TaskStatusFilter = 'open' | 'all' | TaskStatus;

@Component({
  selector: 'app-project-detail-page',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    StatusBadge,
    TaskTable,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.scss',
})
export class ProjectDetailPage implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly taskService = inject(TaskService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  /** Bound from the :id route parameter. */
  readonly id = input.required<string>();

  protected readonly project = signal<ProjectDetails | null>(null);
  protected readonly notFound = signal(false);

  protected readonly statusLabel = projectStatusLabel;
  protected readonly statusTone = projectStatusTone;
  protected readonly taskStatuses = TASK_STATUSES;
  protected readonly taskStatusLabel = taskStatusLabel;

  protected readonly canEditTasks = computed(() => {
    const project = this.project();
    return !!project && project.canManage && !isClosedProject(project.status);
  });

  protected readonly progress = computed(() => {
    const tasks = this.project()?.tasks;
    if (!tasks) {
      return { done: 0, total: 0, percent: 0 };
    }
    const total = tasks.todo + tasks.inProgress + tasks.inReview + tasks.done;
    return { done: tasks.done, total, percent: total === 0 ? 0 : Math.round((tasks.done / total) * 100) };
  });

  protected readonly assignees = computed(() => {
    const project = this.project();
    if (!project) {
      return [];
    }
    return [
      { id: project.managerId, name: `${project.managerName} (manager)` },
      ...project.members.map((m) => ({ id: m.employeeId, name: m.fullName })),
    ];
  });

  protected readonly taskFilters = inject(FormBuilder).nonNullable.group({
    status: 'open' as TaskStatusFilter,
    assigneeId: null as number | null,
  });

  protected readonly tasks = signal<PagedResult<TaskListItem>>(emptyPage());
  protected readonly loadingTasks = signal(false);
  private taskSort: Sort = { active: 'dueDate', direction: 'asc' };
  protected pageIndex = 0;
  protected pageSize = 10;
  private readonly reloadTasks$ = new Subject<void>();

  private get projectId(): number {
    return Number(this.id());
  }

  constructor() {
    this.reloadTasks$
      .pipe(
        tap(() => this.loadingTasks.set(true)),
        switchMap(() => {
          const { status, assigneeId } = this.taskFilters.getRawValue();
          return this.taskService
            .list({
              page: this.pageIndex + 1,
              pageSize: this.pageSize,
              projectId: this.projectId,
              assigneeId,
              status: status === 'open' || status === 'all' ? null : status,
              openOnly: status === 'open',
              sortBy: this.taskSort.active,
              sortDirection: this.taskSort.direction,
            })
            .pipe(catchError(() => of(emptyPage<TaskListItem>())));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((tasks) => {
        this.tasks.set(tasks);
        this.loadingTasks.set(false);
      });

    this.taskFilters.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.pageIndex = 0;
      this.reloadTasks$.next();
    });
  }

  ngOnInit(): void {
    this.loadProject();
    this.reloadTasks$.next();
  }

  protected onTaskSort(sort: Sort): void {
    this.taskSort = sort;
    this.pageIndex = 0;
    this.reloadTasks$.next();
  }

  protected onTaskPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.reloadTasks$.next();
  }

  protected editProject(): void {
    this.dialog
      .open(ProjectDialog, { data: this.project(), width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: ProjectDetails) => {
        if (saved) {
          this.project.set(saved);
          this.reloadTasks$.next();
          this.toast.success('Project updated.');
        }
      });
  }

  protected addMember(): void {
    this.dialog
      .open(AddMemberDialog, { data: this.project(), width: '480px' })
      .afterClosed()
      .subscribe((saved?: ProjectDetails) => {
        if (saved) {
          this.project.set(saved);
          this.toast.success('Member added.');
        }
      });
  }

  protected removeMember(member: ProjectMember): void {
    this.confirmService
      .confirm({
        title: 'Remove member?',
        message: `${member.fullName} will no longer see this project or be assignable to its tasks.`,
        confirmText: 'Remove',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.projectService.removeMember(this.projectId, member.employeeId)),
      )
      .subscribe({
        next: (project) => {
          this.project.set(project);
          this.toast.success(`${member.fullName} removed from the project.`);
        },
        error: (error) => {
          const message = formErrorMessage(error);
          if (message) {
            this.toast.error(message);
          }
        },
      });
  }

  protected newTask(): void {
    const data: TaskDialogData = { projectId: this.projectId, assignees: this.assignees(), task: null };
    this.dialog
      .open(TaskDialog, { data, width: '600px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: TaskDetails) => {
        if (saved) {
          this.toast.success('Task created.');
          this.loadProject();
          this.reloadTasks$.next();
        }
      });
  }

  private loadProject(): void {
    this.projectService.get(this.projectId).subscribe({
      next: (project) => this.project.set(project),
      error: (error: HttpErrorResponse) => this.notFound.set(error.status === 404),
    });
  }
}
