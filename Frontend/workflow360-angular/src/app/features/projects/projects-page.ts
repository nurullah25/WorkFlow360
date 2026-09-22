import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { catchError, debounceTime, of, Subject, switchMap, tap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast.service';
import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { StatusBadge } from '../../shared/status-badge';
import { countLabel } from '../../shared/text';
import { ProjectDialog } from './project-dialog';
import {
  PROJECT_STATUSES,
  ProjectDetails,
  ProjectListItem,
  ProjectQuery,
  ProjectStatus,
  projectStatusLabel,
  projectStatusTone,
} from './project.models';
import { ProjectService } from './project.service';

@Component({
  selector: 'app-projects-page',
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
  ],
  templateUrl: './projects-page.html',
  styles: `
    .progress {
      display: flex;
      align-items: center;
      gap: 8px;
      min-width: 140px;

      mat-progress-bar {
        flex: 1;
        border-radius: 3px;
      }
    }
    .overdue-count {
      color: #a4262c;
      font-weight: 500;
    }
  `,
})
export class ProjectsPage {
  private readonly projectService = inject(ProjectService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly canCreate = inject(AuthService).hasRole('Admin', 'Manager');
  protected readonly countLabel = countLabel;
  protected readonly statuses = PROJECT_STATUSES;
  protected readonly statusLabel = projectStatusLabel;
  protected readonly statusTone = projectStatusTone;
  protected readonly columns = ['project', 'manager', 'status', 'timeline', 'progress', 'overdue'];

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: '',
    status: '' as ProjectStatus | '',
  });

  protected readonly result = signal<PagedResult<ProjectListItem>>(emptyPage());
  protected readonly loading = signal(false);

  private sort: Sort = { active: 'name', direction: 'asc' };
  protected pageIndex = 0;
  protected pageSize = 20;

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() =>
          this.projectService.list(this.buildQuery()).pipe(catchError(() => of(emptyPage<ProjectListItem>()))),
        ),
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

  protected progress(project: ProjectListItem): number {
    return project.taskCount === 0 ? 0 : Math.round((project.doneTaskCount / project.taskCount) * 100);
  }

  protected open(project: ProjectListItem): void {
    this.router.navigate(['/projects', project.id]);
  }

  protected create(): void {
    this.dialog
      .open(ProjectDialog, { data: null, width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: ProjectDetails) => {
        if (saved) {
          this.toast.success(`Project ${saved.code} created.`);
          this.router.navigate(['/projects', saved.id]);
        }
      });
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

  private buildQuery(): ProjectQuery {
    const { search, status } = this.filters.getRawValue();
    return {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      search,
      status,
      sortBy: this.sort.active,
      sortDirection: this.sort.direction,
    };
  }
}
