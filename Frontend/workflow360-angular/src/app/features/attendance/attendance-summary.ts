import { Component, input } from '@angular/core';

import { AttendanceSummary, formatMinutes } from './attendance.models';

@Component({
  selector: 'app-attendance-summary',
  template: `
    <div class="tile">
      <div class="label">Present</div>
      <div class="value">{{ summary().present }} <span>/ {{ summary().workingDays }} days</span></div>
    </div>
    <div class="tile">
      <div class="label">Late</div>
      <div class="value" [class.warn]="summary().late > 0">{{ summary().late }}</div>
    </div>
    <div class="tile">
      <div class="label">On leave</div>
      <div class="value">{{ summary().onLeave }}</div>
    </div>
    <div class="tile">
      <div class="label">Absent</div>
      <div class="value" [class.alert]="summary().absent > 0">{{ summary().absent }}</div>
    </div>
    <div class="tile">
      <div class="label">Hours worked</div>
      <div class="value">{{ formatMinutes(summary().totalWorkedMinutes) }}</div>
    </div>
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 12px;
    }
    .tile {
      padding: 12px 16px;
      border: 1px solid var(--wf-border);
      border-radius: 8px;
      background: var(--wf-surface);
    }
    .label {
      font-size: 12px;
      color: var(--wf-text-muted);
    }
    .value {
      margin-top: 4px;
      font-size: 22px;
      font-weight: 600;

      span {
        font-size: 13px;
        font-weight: 400;
        color: var(--wf-text-muted);
      }
      &.warn {
        color: #8a5a00;
      }
      &.alert {
        color: #a4262c;
      }
    }
  `,
})
export class AttendanceSummaryTiles {
  readonly summary = input.required<AttendanceSummary>();
  protected readonly formatMinutes = formatMinutes;
}
