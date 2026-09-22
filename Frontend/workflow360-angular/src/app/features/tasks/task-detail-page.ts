import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { filter, finalize, switchMap } from 'rxjs';

import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { ConfirmService } from '../../shared/confirm-dialog';
import { StatusBadge } from '../../shared/status-badge';
import { ProjectService } from '../projects/project.service';
import { TaskDialog, TaskDialogData } from './task-dialog';
import {
  describeHistory,
  TaskComment,
  TaskDetails,
  TaskHistoryEntry,
  taskPriorityTone,
  TaskStatus,
  taskStatusLabel,
  taskStatusTone,
} from './task.models';
import { TaskService } from './task.service';

@Component({
  selector: 'app-task-detail-page',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    StatusBadge,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './task-detail-page.html',
  styleUrl: './task-detail-page.scss',
})
export class TaskDetailPage implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly projectService = inject(ProjectService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  /** Bound from the :id route parameter. */
  readonly id = input.required<string>();

  protected readonly task = signal<TaskDetails | null>(null);
  protected readonly comments = signal<TaskComment[]>([]);
  protected readonly history = signal<TaskHistoryEntry[]>([]);
  protected readonly notFound = signal(false);
  protected readonly changingStatus = signal(false);
  protected readonly postingComment = signal(false);

  protected readonly commentControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(2000)],
  });

  protected readonly statusLabel = taskStatusLabel;
  protected readonly statusTone = taskStatusTone;
  protected readonly priorityTone = taskPriorityTone;
  protected readonly describeHistory = describeHistory;

  private get taskId(): number {
    return Number(this.id());
  }

  ngOnInit(): void {
    this.taskService.get(this.taskId).subscribe({
      next: (task) => this.task.set(task),
      error: (error: HttpErrorResponse) => this.notFound.set(error.status === 404),
    });
    this.loadComments();
    this.loadHistory();
  }

  protected changeStatus(status: TaskStatus): void {
    const change$ = this.taskService.changeStatus(this.taskId, status);

    const confirmed$ =
      status === 'Cancelled'
        ? this.confirmService.confirm({
            title: 'Cancel this task?',
            message: 'The task will be closed without being completed. You can restore it later.',
            confirmText: 'Cancel task',
            destructive: true,
          })
        : null;

    const run = () => {
      this.changingStatus.set(true);
      change$.pipe(finalize(() => this.changingStatus.set(false))).subscribe({
        next: (task) => {
          this.task.set(task);
          this.loadHistory();
          this.toast.success(`Moved to ${taskStatusLabel(task.status)}.`);
        },
        error: (error) => this.showError(error),
      });
    };

    if (confirmed$) {
      confirmed$.pipe(filter(Boolean)).subscribe(run);
    } else {
      run();
    }
  }

  protected edit(): void {
    const task = this.task();
    if (!task) {
      return;
    }

    // Only project members and the manager can be assigned, so fetch the current member list first.
    this.projectService
      .get(task.projectId)
      .pipe(
        switchMap((project) => {
          const data: TaskDialogData = {
            projectId: project.id,
            task,
            assignees: [
              { id: project.managerId, name: `${project.managerName} (manager)` },
              ...project.members.map((m) => ({ id: m.employeeId, name: m.fullName })),
            ],
          };
          return this.dialog.open(TaskDialog, { data, width: '600px', maxWidth: '95vw' }).afterClosed();
        }),
      )
      .subscribe((saved?: TaskDetails) => {
        if (saved) {
          this.task.set(saved);
          this.loadHistory();
          this.toast.success('Task updated.');
        }
      });
  }

  protected postComment(): void {
    if (this.commentControl.invalid) {
      return;
    }

    this.postingComment.set(true);
    this.taskService
      .addComment(this.taskId, this.commentControl.value.trim())
      .pipe(finalize(() => this.postingComment.set(false)))
      .subscribe({
        next: (comment) => {
          this.comments.update((list) => [...list, comment]);
          this.commentControl.reset();
        },
        error: (error) => this.showError(error),
      });
  }

  private loadComments(): void {
    this.taskService.comments(this.taskId).subscribe((comments) => this.comments.set(comments));
  }

  private loadHistory(): void {
    this.taskService.history(this.taskId).subscribe((history) => this.history.set(history));
  }

  private showError(error: unknown): void {
    const message = formErrorMessage(error);
    if (message) {
      this.toast.error(message);
    }
  }
}
