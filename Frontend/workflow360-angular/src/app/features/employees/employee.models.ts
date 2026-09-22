import { BadgeTone } from '../../shared/status-badge';

export type EmploymentStatus = 'Probation' | 'Active' | 'Resigned' | 'Terminated';

export const EMPLOYMENT_STATUSES: EmploymentStatus[] = ['Probation', 'Active', 'Resigned', 'Terminated'];

export function isFormerStatus(status: EmploymentStatus): boolean {
  return status === 'Resigned' || status === 'Terminated';
}

export function employmentStatusTone(status: EmploymentStatus): BadgeTone {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Probation':
      return 'info';
    default:
      return 'neutral';
  }
}

export interface EmployeeListItem {
  id: number;
  employeeCode: string;
  fullName: string;
  email: string;
  departmentName: string;
  designationTitle: string;
  managerName: string | null;
  joiningDate: string;
  status: EmploymentStatus;
}

export interface EmployeeDetails {
  id: number;
  employeeCode: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  departmentId: number;
  departmentName: string;
  designationId: number;
  designationTitle: string;
  managerId: number | null;
  managerName: string | null;
  joiningDate: string;
  status: EmploymentStatus;
  userId: number | null;
  directReportCount: number;
}

export interface EmployeeLookup {
  id: number;
  fullName: string;
  employeeCode: string;
  email: string;
}

export interface SaveEmployee {
  employeeCode: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  departmentId: number;
  designationId: number;
  managerId: number | null;
  joiningDate: string;
  status: EmploymentStatus;
}

export interface EmployeeQuery {
  page: number;
  pageSize: number;
  search?: string;
  departmentId?: number | null;
  status?: EmploymentStatus | null;
  includeFormer?: boolean;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc' | '';
}
