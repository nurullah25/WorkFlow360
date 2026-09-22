import { LeaveBalance, LeaveStatus } from '../leave/leave.models';
import { TaskListItem } from '../tasks/task.models';

export interface Dashboard {
  company: CompanyOverview | null;
  team: TeamOverview | null;
  me: MyOverview | null;
  upcomingHolidays: { id: number; date: string; name: string }[];
}

export interface CompanyOverview {
  activeEmployees: number;
  checkedInToday: number;
  lateToday: number;
  onLeaveToday: number;
  pendingLeaveRequests: number;
  activeProjects: number;
  openTasks: number;
  overdueTasks: number;
  headcount: { department: string; employees: number }[];
}

export interface TeamOverview {
  directReports: number;
  checkedInToday: number;
  onLeaveToday: number;
  pendingApprovals: number;
  projects: ProjectProgress[];
}

export interface ProjectProgress {
  id: number;
  code: string;
  name: string;
  doneTasks: number;
  totalTasks: number;
  overdueTasks: number;
}

export interface MyOverview {
  checkInAt: string | null;
  checkOutAt: string | null;
  isLate: boolean;
  isWorkingDay: boolean;
  onLeaveToday: string | null;
  openTasks: number;
  overdueTasks: number;
  dueSoon: TaskListItem[];
  leaveBalances: LeaveBalance[];
  upcomingLeave: { id: number; leaveTypeName: string; startDate: string; endDate: string; totalDays: number; status: LeaveStatus }[];
}
