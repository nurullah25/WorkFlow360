import { BadgeTone } from '../../shared/status-badge';

export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type LeaveScope = 'mine' | 'approvals' | 'team';

export const LEAVE_STATUSES: LeaveStatus[] = ['Pending', 'Approved', 'Rejected', 'Cancelled'];

export function leaveStatusTone(status: LeaveStatus): BadgeTone {
  switch (status) {
    case 'Pending':
      return 'warning';
    case 'Approved':
      return 'success';
    case 'Rejected':
      return 'danger';
    default:
      return 'neutral';
  }
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  leaveTypeId: number;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  totalDays: number;
  reason: string;
  status: LeaveStatus;
  reviewedByName: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
  createdAt: string;
  canReview: boolean;
  canCancel: boolean;
}

export interface LeaveBalance {
  id: number;
  leaveTypeId: number;
  leaveTypeCode: string;
  leaveTypeName: string;
  year: number;
  allocatedDays: number;
  usedDays: number;
  pendingDays: number;
  availableDays: number;
}

export interface LeavePreview {
  workingDays: number;
  availableDays: number | null;
}

export interface LeaveType {
  id: number;
  code: string;
  name: string;
  defaultDaysPerYear: number;
  isPaid: boolean;
  isActive: boolean;
}

export interface Holiday {
  id: number;
  date: string;
  name: string;
}

export interface EmployeeLeaveBalance {
  id: number;
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  leaveTypeName: string;
  year: number;
  allocatedDays: number;
  usedDays: number;
  pendingDays: number;
}

export interface LeaveRequestQuery {
  page: number;
  pageSize: number;
  scope: LeaveScope;
  status?: LeaveStatus | '';
  search?: string;
  year?: number | null;
}

export interface LeaveBalanceQuery {
  page: number;
  pageSize: number;
  year: number;
  leaveTypeId?: number | null;
  search?: string;
}
