import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';

import { ToastService } from '../../core/toast.service';
import { PageHeader } from '../../shared/page-header';
import { StatusBadge } from '../../shared/status-badge';
import { DepartmentDialog } from './department-dialog';
import { DesignationDialog } from './designation-dialog';
import { Department, Designation, OrganisationService } from './organisation.service';

@Component({
  selector: 'app-organisation-page',
  imports: [
    PageHeader,
    StatusBadge,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './organisation-page.html',
  styles: `
    .tab-toolbar {
      display: flex;
      justify-content: flex-end;
      padding: 12px 16px;
    }
  `,
})
export class OrganisationPage {
  private readonly organisationService = inject(OrganisationService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  protected readonly departments = signal<Department[]>([]);
  protected readonly designations = signal<Designation[]>([]);
  protected readonly loadingDepartments = signal(false);
  protected readonly loadingDesignations = signal(false);

  protected readonly departmentColumns = ['code', 'name', 'employeeCount', 'status', 'actions'];
  protected readonly designationColumns = ['title', 'employeeCount', 'status', 'actions'];

  constructor() {
    this.loadDepartments();
    this.loadDesignations();
  }

  protected editDepartment(department: Department | null): void {
    this.dialog
      .open(DepartmentDialog, { data: department, width: '520px' })
      .afterClosed()
      .subscribe((saved?: Department) => {
        if (saved) {
          this.toast.success(`Department ${saved.name} saved.`);
          this.loadDepartments();
        }
      });
  }

  protected editDesignation(designation: Designation | null): void {
    this.dialog
      .open(DesignationDialog, { data: designation, width: '440px' })
      .afterClosed()
      .subscribe((saved?: Designation) => {
        if (saved) {
          this.toast.success(`Designation ${saved.title} saved.`);
          this.loadDesignations();
        }
      });
  }

  private loadDepartments(): void {
    this.loadingDepartments.set(true);
    this.organisationService
      .departments(true)
      .pipe(finalize(() => this.loadingDepartments.set(false)))
      .subscribe((departments) => this.departments.set(departments));
  }

  private loadDesignations(): void {
    this.loadingDesignations.set(true);
    this.organisationService
      .designations(true)
      .pipe(finalize(() => this.loadingDesignations.set(false)))
      .subscribe((designations) => this.designations.set(designations));
  }
}
