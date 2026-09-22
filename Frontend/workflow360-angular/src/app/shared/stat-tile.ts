import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * One headline number. `tone` is only for real warning states (late, overdue, absent) and is always paired
 * with the label, never used to tell tiles apart.
 */
@Component({
  selector: 'app-stat-tile',
  imports: [NgTemplateOutlet, RouterLink],
  template: `
    @if (link(); as link) {
      <a class="tile clickable" [routerLink]="link" [queryParams]="queryParams()">
        <ng-container [ngTemplateOutlet]="content" />
      </a>
    } @else {
      <div class="tile">
        <ng-container [ngTemplateOutlet]="content" />
      </div>
    }
    <ng-template #content>
      <div class="label">{{ label() }}</div>
      <div class="value" [class]="'tone-' + tone()">{{ value() }}</div>
      @if (hint()) {
        <div class="hint">{{ hint() }}</div>
      }
    </ng-template>
  `,
  styles: `
    .tile {
      display: block;
      height: 100%;
      box-sizing: border-box;
      padding: 14px 16px;
      border: 1px solid var(--wf-border);
      border-radius: 8px;
      background: var(--wf-surface);
      color: inherit;
      text-decoration: none;
    }
    .clickable:hover {
      border-color: var(--mat-sys-primary);
    }
    .label {
      font-size: 13px;
      color: var(--wf-text-muted);
    }
    .value {
      margin-top: 4px;
      font-size: 26px;
      font-weight: 600;
      line-height: 1.2;
    }
    .tone-warning {
      color: #8a5a00;
    }
    .tone-danger {
      color: #a4262c;
    }
    .hint {
      margin-top: 2px;
      font-size: 12px;
      color: var(--wf-text-muted);
    }
  `,
})
export class StatTile {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly hint = input<string>();
  readonly tone = input<'default' | 'warning' | 'danger'>('default');
  readonly link = input<string>();
  readonly queryParams = input<Record<string, string>>();
}
