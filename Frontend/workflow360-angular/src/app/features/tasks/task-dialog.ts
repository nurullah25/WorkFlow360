import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';

import { applyServerErrors, formErrorMessage } from '../../core/http/api-error';
import { parseIsoDate, toIsoDate } from '../../shared/dates';
import { fieldError } from '../../shared/form-errors';
import { SaveTask, TASK_PRIORITIES, TaskDetails, TaskPriority } from './task.models';
import { TaskService } from './task.service';

export interface TaskDialogData {
  projectId: number;
  /** Project members plus the project manager — the only people a task can be assigned to. */
  assignees: { id: number; name: string }[];
  task: TaskDetails | null;
}

@Component({
  selector: 'app-task-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.task ? 'Edit task' : 'New task' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (errorMessage()) {
          <div class="form-error-banner">{{ errorMessage() }}</div>
        }
        <div class="form-grid">
          <mat-form-field class="full-width">
            <mat-label>Title</mat-label>
            <input matInput formControlName="title" maxlength="200" />
            <mat-error>{{ fieldError(form.controls.title) }}</mat-error>
          </mat-form-field>

          <mat-form-field class="full-width">
            <mat-label>Description</mat-label>
            <textarea matInput formControlName="description" rows="4"></textarea>
            <mat-error>{{ fieldError(form.controls.description) }}</mat-error>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Assignee</mat-label>
            <mat-select formControlName="assigneeId">
              <mat-option [value]="null">Unassigned</mat-option>
              @for (person of data.assignees; track person.id) {
                <mat-option [value]="person.id">{{ person.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              @for (priority of priorities; track priority) {
                <mat-option [value]="priority">{{ priority }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Due date</mat-label>
            <input matInput [matDatepicker]="duePicker" formControlName="dueDate" />
            <mat-datepicker-toggle matIconSuffix [for]="duePicker" />
            <mat-datepicker #duePicker />
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class TaskDialog {
  private readonly taskService = inject(TaskService);
  private readonly dialogRef = inject(MatDialogRef<TaskDialog, TaskDetails>);
  protected readonly data = inject<TaskDialogData>(MAT_DIALOG_DATA);

  protected readonly fieldError = fieldError;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly task = this.data.task;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: [this.task?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.task?.description ?? '', Validators.maxLength(4000)],
    assigneeId: [this.task?.assigneeId ?? (null as number | null)],
    priority: [this.task?.priority ?? ('Medium' as TaskPriority)],
    dueDate: [this.task?.dueDate ? parseIsoDate(this.task.dueDate) : (null as Date | null)],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: SaveTask = {
      title: value.title,
      description: value.description || null,
      assigneeId: value.assigneeId,
      priority: value.priority,
      dueDate: value.dueDate ? toIsoDate(value.dueDate) : null,
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const save$ = this.task
      ? this.taskService.update(this.task.id, request)
      : this.taskService.create(this.data.projectId, request);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error) => {
        if (!applyServerErrors(this.form, error)) {
          this.errorMessage.set(formErrorMessage(error));
        }
      },
    });
  }
}
