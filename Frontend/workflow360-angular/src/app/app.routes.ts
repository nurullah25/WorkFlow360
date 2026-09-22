import { Routes } from '@angular/router';

import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in | WorkFlow360',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard | WorkFlow360',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
      },
      {
        path: 'projects',
        title: 'Projects | WorkFlow360',
        loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
      },
      {
        path: 'projects/:id',
        title: 'Project | WorkFlow360',
        loadComponent: () =>
          import('./features/projects/project-detail-page').then((m) => m.ProjectDetailPage),
      },
      {
        path: 'tasks',
        title: 'Tasks | WorkFlow360',
        loadComponent: () => import('./features/tasks/tasks-page').then((m) => m.TasksPage),
      },
      {
        path: 'tasks/:id',
        title: 'Task | WorkFlow360',
        loadComponent: () =>
          import('./features/tasks/task-detail-page').then((m) => m.TaskDetailPage),
      },
      {
        path: 'attendance',
        title: 'Attendance | WorkFlow360',
        loadComponent: () =>
          import('./features/attendance/attendance-page').then((m) => m.AttendancePage),
      },
      {
        path: 'leave',
        title: 'Leave | WorkFlow360',
        loadComponent: () => import('./features/leave/leave-page').then((m) => m.LeavePage),
      },
      {
        path: 'leave-settings',
        title: 'Leave settings | WorkFlow360',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'HR'] },
        loadComponent: () =>
          import('./features/leave/leave-settings-page').then((m) => m.LeaveSettingsPage),
      },
      {
        path: 'employees',
        title: 'Employees | WorkFlow360',
        loadComponent: () =>
          import('./features/employees/employees-page').then((m) => m.EmployeesPage),
      },
      {
        path: 'departments',
        title: 'Departments | WorkFlow360',
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'HR'] },
        loadComponent: () =>
          import('./features/organisation/organisation-page').then((m) => m.OrganisationPage),
      },
      {
        path: 'users',
        title: 'Users | WorkFlow360',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/users/users-page').then((m) => m.UsersPage),
      },
      {
        path: 'audit-log',
        title: 'Audit log | WorkFlow360',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/audit-log/audit-log-page').then((m) => m.AuditLogPage),
      },
      {
        path: 'notifications',
        title: 'Notifications | WorkFlow360',
        loadComponent: () =>
          import('./features/notifications/notifications-page').then((m) => m.NotificationsPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
