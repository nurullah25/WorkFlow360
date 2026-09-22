import { BadgeTone } from '../../shared/status-badge';

export type ProjectStatus = 'Planned' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled';

export const PROJECT_STATUSES: ProjectStatus[] = ['Planned', 'Active', 'OnHold', 'Completed', 'Cancelled'];

export function projectStatusLabel(status: ProjectStatus): string {
  return status === 'OnHold' ? 'On hold' : status;
}

export function projectStatusTone(status: ProjectStatus): BadgeTone {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Planned':
      return 'info';
    case 'OnHold':
      return 'warning';
    default:
      return 'neutral';
  }
}

export function isClosedProject(status: ProjectStatus): boolean {
  return status === 'Completed' || status === 'Cancelled';
}

export interface ProjectListItem {
  id: number;
  code: string;
  name: string;
  managerName: string;
  status: ProjectStatus;
  startDate: string;
  endDate: string | null;
  memberCount: number;
  taskCount: number;
  doneTaskCount: number;
  overdueTaskCount: number;
}

export interface ProjectMember {
  employeeId: number;
  fullName: string;
  employeeCode: string;
  designationTitle: string;
  roleInProject: string | null;
  addedAt: string;
}

export interface TaskSummary {
  todo: number;
  inProgress: number;
  inReview: number;
  done: number;
  cancelled: number;
  overdue: number;
}

export interface ProjectDetails {
  id: number;
  code: string;
  name: string;
  description: string | null;
  managerId: number;
  managerName: string;
  startDate: string;
  endDate: string | null;
  status: ProjectStatus;
  members: ProjectMember[];
  tasks: TaskSummary;
  canManage: boolean;
}

export interface SaveProject {
  code: string;
  name: string;
  description: string | null;
  managerId: number;
  startDate: string;
  endDate: string | null;
  status: ProjectStatus;
}

export interface ProjectQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: ProjectStatus | '';
  sortBy?: string;
  sortDirection?: 'asc' | 'desc' | '';
}
