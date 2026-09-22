import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, filter, of, Subject, switchMap, tap } from 'rxjs';

import { Role } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { formErrorMessage } from '../../core/http/api-error';
import { ToastService } from '../../core/toast.service';
import { ConfirmService } from '../../shared/confirm-dialog';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { ResetPasswordDialog } from './reset-password-dialog';
import { UserDialog } from './user-dialog';
import { ROLES, UserListItem, UserQuery, UserService } from './user.service';

@Component({
  selector: 'app-users-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    PageHeader,
    StatusBadge,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  templateUrl: './users-page.html',
})
export class UsersPage {
  private readonly userService = inject(UserService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly currentUserId = inject(AuthService).user()?.id;
  protected readonly roles = ROLES;
  protected readonly columns = ['user', 'role', 'employee', 'status', 'lastLoginAt', 'actions'];

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    role: '' as Role | '',
    status: '' as '' | 'active' | 'inactive',
  });

  protected readonly result = signal<PagedResult<UserListItem>>(emptyPage());
  protected readonly loading = signal(false);

  private sort: Sort = { active: 'name', direction: 'asc' };
  protected pageIndex = 0;
  protected pageSize = 20;

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() => this.userService.list(this.buildQuery()).pipe(catchError(() => of(emptyPage<UserListItem>())))),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed()).subscribe(() => {
      this.pageIndex = 0;
      this.reload$.next();
    });

    this.reload$.next();
  }

  protected onSort(sort: Sort): void {
    this.sort = sort;
    this.pageIndex = 0;
    this.reload$.next();
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.reload$.next();
  }

  protected openDialog(user: UserListItem | null): void {
    this.dialog
      .open(UserDialog, { data: user, width: '600px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: UserListItem) => {
        if (saved) {
          this.toast.success(user ? `${saved.fullName} updated.` : `Account created for ${saved.fullName}.`);
          this.reload$.next();
        }
      });
  }

  protected resetPassword(user: UserListItem): void {
    this.dialog
      .open(ResetPasswordDialog, { data: user, width: '440px' })
      .afterClosed()
      .subscribe((done?: boolean) => {
        if (done) {
          this.toast.success(`Password reset for ${user.fullName}.`);
        }
      });
  }

  protected toggleActive(user: UserListItem): void {
    const activating = !user.isActive;

    this.confirmService
      .confirm({
        title: activating ? 'Activate account?' : 'Deactivate account?',
        message: activating
          ? `${user.fullName} will be able to sign in again.`
          : `${user.fullName} will be signed out and won't be able to sign in until the account is activated again.`,
        confirmText: activating ? 'Activate' : 'Deactivate',
        destructive: !activating,
      })
      .pipe(
        filter(Boolean),
        switchMap(() =>
          this.userService.update(user.id, { fullName: user.fullName, role: user.role, isActive: activating }),
        ),
      )
      .subscribe({
        next: (saved) => {
          this.toast.success(`${saved.fullName} ${saved.isActive ? 'activated' : 'deactivated'}.`);
          this.reload$.next();
        },
        error: (error) => {
          const message = formErrorMessage(error);
          if (message) {
            this.toast.error(message);
          }
        },
      });
  }

  private buildQuery(): UserQuery {
    const { search, role, status } = this.filters.getRawValue();
    return {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      search,
      role,
      isActive: status === '' ? null : status === 'active',
      sortBy: this.sort.active,
      sortDirection: this.sort.direction,
    };
  }
}
