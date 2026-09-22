import { BadgeTone } from '../../shared/status-badge';

export type AttendanceDayStatus =
  | 'Present'
  | 'Late'
  | 'OnLeave'
  | 'Holiday'
  | 'Weekend'
  | 'Absent'
  | 'Upcoming'
  | 'NotEmployed';

const STATUS_LABELS: Record<AttendanceDayStatus, string> = {
  Present: 'Present',
  Late: 'Late',
  OnLeave: 'On leave',
  Holiday: 'Holiday',
  Weekend: 'Weekend',
  Absent: 'Absent',
  Upcoming: '—',
  NotEmployed: 'Not yet joined',
};

export function attendanceStatusLabel(status: AttendanceDayStatus): string {
  return STATUS_LABELS[status];
}

export function attendanceStatusTone(status: AttendanceDayStatus): BadgeTone {
  switch (status) {
    case 'Present':
      return 'success';
    case 'Late':
      return 'warning';
    case 'OnLeave':
      return 'info';
    case 'Absent':
      return 'danger';
    default:
      return 'neutral';
  }
}

/** Days up to and including today, newest first — the future adds nothing to an attendance list. */
export function pastDaysNewestFirst(days: AttendanceDay[], today: string): AttendanceDay[] {
  return days.filter((day) => day.date <= today).reverse();
}

/** 510 -> "8h 30m" */
export function formatMinutes(minutes: number | null | undefined): string {
  if (minutes === null || minutes === undefined) {
    return '—';
  }
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return hours > 0 ? `${hours}h ${rest}m` : `${rest}m`;
}

export interface TodayAttendance {
  date: string;
  isWorkingDay: boolean;
  holidayName: string | null;
  leaveTypeName: string | null;
  checkInAt: string | null;
  checkOutAt: string | null;
  isLate: boolean;
  workedMinutes: number | null;
  officeStartTime: string;
  graceMinutes: number;
}

export interface AttendanceDay {
  date: string;
  status: AttendanceDayStatus;
  isWorkingDay: boolean;
  checkInAt: string | null;
  checkOutAt: string | null;
  workedMinutes: number | null;
  note: string | null;
}

export interface AttendanceSummary {
  workingDays: number;
  present: number;
  late: number;
  onLeave: number;
  absent: number;
  holidays: number;
  totalWorkedMinutes: number;
}

export interface EmployeeMonth {
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  year: number;
  month: number;
  summary: AttendanceSummary;
  days: AttendanceDay[];
}

export interface TeamAttendanceRow {
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  departmentName: string;
  summary: AttendanceSummary;
}

export interface YearMonth {
  year: number;
  month: number;
}
