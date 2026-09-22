import { DatePipe } from '@angular/common';
import { Component, computed, model } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { YearMonth } from './attendance.models';

/** "‹ March 2026 ›" — two-way bound with [(period)]. Future months are disabled. */
@Component({
  selector: 'app-month-switcher',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  template: `
    <button mat-icon-button (click)="move(-1)" aria-label="Previous month">
      <mat-icon>chevron_left</mat-icon>
    </button>
    <span class="label">{{ firstDay() | date: 'MMMM yyyy' }}</span>
    <button mat-icon-button (click)="move(1)" [disabled]="isCurrentMonth()" aria-label="Next month">
      <mat-icon>chevron_right</mat-icon>
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .label {
      min-width: 120px;
      text-align: center;
      font-weight: 600;
    }
  `,
})
export class MonthSwitcher {
  readonly period = model.required<YearMonth>();

  protected readonly firstDay = computed(() => new Date(this.period().year, this.period().month - 1, 1));

  protected readonly isCurrentMonth = computed(() => {
    const now = new Date();
    return this.period().year === now.getFullYear() && this.period().month === now.getMonth() + 1;
  });

  protected move(offset: number): void {
    const date = new Date(this.period().year, this.period().month - 1 + offset, 1);
    this.period.set({ year: date.getFullYear(), month: date.getMonth() + 1 });
  }
}
