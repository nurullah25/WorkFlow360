import { Component, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'app-status-badge',
  template: '{{ label() }}',
  host: { '[class]': '"tone-" + tone()' },
  styles: `
    :host {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 10px;
      font-size: 12px;
      font-weight: 500;
      line-height: 18px;
      white-space: nowrap;
    }
    :host(.tone-neutral) { background: #eef0f3; color: #4a5565; }
    :host(.tone-info) { background: #e6f0fb; color: #1d5a9c; }
    :host(.tone-success) { background: #e5f4ea; color: #1f6f43; }
    :host(.tone-warning) { background: #fdf2dd; color: #8a5a00; }
    :host(.tone-danger) { background: #fbe9ea; color: #a4262c; }
  `,
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<BadgeTone>('neutral');
}
