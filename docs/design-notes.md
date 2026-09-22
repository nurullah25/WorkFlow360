# WorkFlow360 – Design Notes

Working notes on the decisions behind the project. The README covers setup; this file covers the *why*.

## Roles

| Role | Purpose |
|---|---|
| Admin | System owner. Manages user accounts and roles, can see and do everything. |
| HR | Owns people data: employees, departments, designations, leave types, holidays and balances. Company-wide view of attendance and leave. |
| Manager | Runs projects and a team. Approves leave for direct reports. |
| Employee | Works on assigned tasks, records attendance, requests leave. |

A user has exactly one role. Multi-role users weren't worth the extra join for this scope.

### Authorization matrix

| Action | Admin | HR | Manager | Employee |
|---|:-:|:-:|:-:|:-:|
| Manage users and roles | ✅ | | | |
| Manage employees, departments, designations | ✅ | ✅ | | |
| Manage leave types, holidays, leave balances | ✅ | ✅ | | |
| Create projects | ✅ | | ✅ | |
| Edit project / members | ✅ | | own projects | |
| Create and assign tasks | ✅ | | own projects | |
| Change task status | ✅ | | own projects | assigned tasks |
| Approve / reject leave | ✅ | employees with no manager | direct reports | |
| View attendance | all | all | team | own |
| View leave requests | all | all | team | own |
| Submit leave, check in / out | ✅* | ✅ | ✅ | ✅ |
| View audit log | ✅ | | | |

\* Only when the user is linked to an employee record.

Role checks use `[Authorize(Roles = ...)]` on controllers. Ownership rules ("is this my direct report?", "do I manage this project?") are checked in the Application services, where the data needed to answer them is already loaded.

## Key decisions

- **Layers:** Domain → Application → Infrastructure → API. Application defines `IAppDbContext`; Infrastructure implements it. No repository layer on top of EF Core.
- **No MediatR / AutoMapper.** Plain services per feature, manual projections with `Select`.
- **Business errors are exceptions** (`NotFoundException`, `BusinessRuleException`, `ForbiddenAccessException`) mapped to ProblemDetails by one middleware.
- **Auth:** short-lived JWT access token kept in memory on the client; refresh token stored hashed in the DB, sent as an HttpOnly cookie, rotated on every use. Password hashing via `PasswordHasher<T>` without the full Identity schema.
- **Audit entries are written explicitly** by services, so they record business events (`LeaveApproved`) rather than raw column diffs.
- **Notifications are polled**, not pushed. SignalR is a possible later improvement.
- **Leave approver** is resolved from `Employee.ManagerId` at review time, not stored on the request. If a manager changes, pending requests follow the new manager.
- **Actor rule:** columns describing *who performed an action* reference `Users`; columns describing *the workforce* reference `Employees`.
- **Tasks table is `ProjectTasks`** to avoid a `Task` entity clashing with `System.Threading.Tasks.Task`.
- **Refresh token reuse:** a rotated token presented again after a 30-second grace window revokes every session for that user (likely theft). Inside the window it is treated as two tabs refreshing at once.

- **Audit rows for inserts** need the new row's id, so create operations save inside an explicit transaction: save the entity, add the audit entry, save again, commit. Updates need only one `SaveChangesAsync`.
- **No hard deletes for people data.** Employees end employment (`Resigned`/`Terminated`), users and departments are deactivated. Ending employment is blocked while the person still has direct reports, and it deactivates their account and revokes their sessions.
- **Reporting lines** are checked for loops by walking up from the proposed manager before saving.
- **Admin lock-out protection:** admins cannot remove their own admin role or deactivate themselves, and the last active admin cannot be demoted.
- **Sorting is whitelisted** per list (`switch` on `sortBy`), not dynamic LINQ, so only indexed/meaningful columns can be sorted. Because enums are stored as text, priority and status sort by an explicit rank (`CASE` in SQL), not alphabetically.
- **Project visibility** lives in one place (`ProjectAccess`). Admin/HR see every project, others see projects they manage or belong to. Tasks inherit visibility from their project. Projects and tasks outside a user's reach return 404, not 403, so ids can't be probed.
- **Task workflow** is a small pure class in the Domain (`TaskWorkflow`) with its own unit tests; the service decides *who* is asking (assignee vs project manager) and the workflow decides *what* is allowed.
- **Closing a project:** completing requires no open tasks; cancelling cancels the open tasks and records why in their history.
- **Notifications** are queued on the same DbContext as the change that caused them, so they're committed atomically. People are never notified about their own actions.
- **Leave balances** store only `Allocated` and `Used`. "Pending" is summed from pending requests when needed, so it can never drift out of sync. Available = allocated − used − pending, and that's what a new request is checked against.
- **Leave approver** is worked out at review time from `Employee.ManagerId` (HR when there is none; Admin can always step in). No approver column means a change of manager automatically re-routes pending requests.
- **Concurrent reviews:** `LeaveRequest.Status` and `LeaveBalance.UsedDays` are EF concurrency tokens. EF adds the original value to the `UPDATE`'s `WHERE`, so if two reviewers act at once the second update affects no rows and fails with `DbUpdateConcurrencyException` → `409`. No locks, no schema change.
- **Attendance days are derived, not stored.** Only real check-ins are rows in the database. "Absent", "on leave", "holiday" and "weekend" are worked out when a month is viewed (`AttendanceCalendar`, a pure function with its own tests), so fixing a holiday or approving leave late never leaves stale absence records behind.
- **Team attendance** pages over employees first, then loads records, approved leave and holidays for that page in three queries — not one query per employee.
- **Working days** are a pure function in the Domain (`WorkingDays.Count`); `BusinessCalendar` supplies the configured weekend and the holidays table.
- **Tests run on SQLite**, which can't `SUM` decimals, so the test-only `DbContext` stores decimals as `REAL`. SQL Server keeps `decimal(5,1)`.
- **Company time zone** (`Company:TimeZone`) defines "today" for overdue tasks, leave and attendance. Timestamps stay UTC; EF marks every `DateTime` read from SQL Server as UTC so the API always serializes them with `Z`.

- **Dashboard** is one endpoint that returns only the sections the caller is entitled to (`company` / `team` / `me`, each nullable). Every figure is a COUNT or small TOP-N on indexed columns, and it reuses `LeaveService`/`TaskService` for balances and "due soon" rather than duplicating their rules.
- **Audit log filter** lists action names from the `AuditActions` constants instead of `SELECT DISTINCT` over an ever-growing table.

## Frontend

- **Angular 21** with standalone components and signals for local state; RxJS for HTTP.
- **Access token in memory only.** On page load, `restoreSession()` calls `/api/auth/refresh` (cookie-based) before the first route guard runs.
- **Auth interceptor** attaches the token to `/api/*` calls and, on 401, refreshes once and retries. Parallel 401s share a single refresh call.
- **Error interceptor** only toasts errors no screen can handle (offline, 403, 429, 5xx). Validation and business errors are shown by the component, next to the form.
- **Dev proxy** (`proxy.conf.json`) keeps the API and client on one origin in development, so no CORS configuration is needed.
- **Lists use `mat-table` directly** rather than a generic config-driven table wrapper; shared pieces are small (page header, status badge, confirm dialog, global list styles).
- **List pages** push filter/sort/page changes into a `Subject` piped through `switchMap`, so a slow response for an old filter can never overwrite a newer one.
- **Notification polling** (unread count) runs once a minute only while the tab is visible, refreshes immediately when the user returns to the tab or opens the bell, and uses `exhaustMap` so a slow request is never stacked. With 1,000 users that is at most ~17 cheap indexed COUNTs per second, and far fewer in practice. SignalR would remove the polling entirely; it's listed as a future improvement.
- **Dashboard charts** are plain HTML/CSS: KPI stat tiles, single-hue horizontal bars for headcount, meters for project progress. No chart library for a handful of bars; warning colours are reserved for real warning states and always paired with a label.
- **Server validation errors** (`ValidationProblemDetails`) are mapped onto the matching form controls; other 400/404/409 errors show in a banner inside the dialog.
