import { Role } from '../core/auth/auth.models';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  /** Leave empty to show the item to every signed-in user. */
  roles?: Role[];
}

export interface NavSection {
  label: string;
  items: NavItem[];
}

export const NAV_SECTIONS: NavSection[] = [
  {
    label: 'Overview',
    items: [{ label: 'Dashboard', icon: 'space_dashboard', route: '/dashboard' }],
  },
  {
    label: 'Work',
    items: [
      { label: 'Projects', icon: 'folder_open', route: '/projects' },
      { label: 'Tasks', icon: 'task_alt', route: '/tasks' },
    ],
  },
  {
    label: 'Time',
    items: [
      { label: 'Attendance', icon: 'schedule', route: '/attendance' },
      { label: 'Leave', icon: 'event_available', route: '/leave' },
    ],
  },
  {
    label: 'People',
    items: [
      { label: 'Employees', icon: 'badge', route: '/employees' },
      { label: 'Departments', icon: 'apartment', route: '/departments', roles: ['Admin', 'HR'] },
    ],
  },
  {
    label: 'Administration',
    items: [
      { label: 'Users', icon: 'manage_accounts', route: '/users', roles: ['Admin'] },
      { label: 'Leave settings', icon: 'tune', route: '/leave-settings', roles: ['Admin', 'HR'] },
      { label: 'Audit log', icon: 'history', route: '/audit-log', roles: ['Admin'] },
    ],
  },
];
