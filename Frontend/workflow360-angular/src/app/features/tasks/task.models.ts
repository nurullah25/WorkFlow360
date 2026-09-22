import { BadgeTone } from '../../shared/status-badge';

export type TaskStatus = 'Todo' | 'InProgress' | 'InReview' | 'Done' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const TASK_STATUSES: TaskStatus[] = ['Todo', 'InProgress', 'InReview', 'Done', 'Cancelled'];
export const TASK_PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

const STATUS_LABELS: Record<TaskStatus, string> = {
  Todo: 'To do',
  InProgress: 'In progress',
  InReview: 'In review',
  Done: 'Done',
  Cancelled: 'Cancelled',
};

export function taskStatusLabel(status: TaskStatus): string {
  return STATUS_LABELS[status];
}

export function taskStatusTone(status: TaskStatus): BadgeTone {
  switch (status) {
    case 'InProgress':
      return 'info';
    case 'InReview':
      return 'warning';
    case 'Done':
      return 'success';
    default:
      return 'neutral';
  }
}

export function taskPriorityTone(priority: TaskPriority): BadgeTone {
  switch (priority) {
    case 'Critical':
      return 'danger';
    case 'High':
      return 'warning';
    default:
      return 'neutral';
  }
}

export interface TaskListItem {
  id: number;
  title: string;
  projectId: number;
  projectCode: string;
  assigneeId: number | null;
  assigneeName: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  dueDate: string | null;
  isOverdue: boolean;
  lastUpdatedAt: string;
}

export interface TaskDetails {
  id: number;
  projectId: number;
  projectCode: string;
  projectName: string;
  title: string;
  description: string | null;
  assigneeId: number | null;
  assigneeName: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  dueDate: string | null;
  completedAt: string | null;
  createdByName: string;
  createdAt: string;
  updatedAt: string | null;
  isOverdue: boolean;
  canEdit: boolean;
  allowedStatuses: TaskStatus[];
}

export interface TaskComment {
  id: number;
  authorUserId: number;
  authorName: string;
  body: string;
  createdAt: string;
}

export interface TaskHistoryEntry {
  id: number;
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  changedByName: string;
  changedAt: string;
}

export interface SaveTask {
  title: string;
  description: string | null;
  assigneeId: number | null;
  priority: TaskPriority;
  dueDate: string | null;
}

export interface TaskQuery {
  page: number;
  pageSize: number;
  search?: string;
  projectId?: number | null;
  assigneeId?: number | null;
  assignedToMe?: boolean;
  status?: TaskStatus | null;
  priority?: TaskPriority | null;
  openOnly?: boolean;
  overdueOnly?: boolean;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc' | '';
}

/** One readable line per history entry, e.g. "changed status from To do to In progress". */
export function describeHistory(entry: TaskHistoryEntry): string {
  const statusOrValue = (value: string | null) =>
    value && value in STATUS_LABELS ? STATUS_LABELS[value as TaskStatus] : (value ?? 'none');

  switch (entry.fieldName) {
    case 'Created':
      return 'created the task';
    case 'Description':
      return 'updated the description';
    case 'Assignee':
      return entry.newValue ? `assigned the task to ${entry.newValue}` : 'unassigned the task';
    case 'DueDate':
      return `changed the due date from ${entry.oldValue ?? 'none'} to ${entry.newValue ?? 'none'}`;
    default:
      return `changed ${entry.fieldName.toLowerCase()} from ${statusOrValue(entry.oldValue)} to ${statusOrValue(entry.newValue)}`;
  }
}
