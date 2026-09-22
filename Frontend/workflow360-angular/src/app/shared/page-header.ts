import { Component, input } from '@angular/core';

/** Page title row. Action buttons go inside the element and are shown on the right. */
@Component({
  selector: 'app-page-header',
  template: `
    <div class="text">
      <h1>{{ heading() }}</h1>
      @if (subtitle()) {
        <p>{{ subtitle() }}</p>
      }
    </div>
    <div class="actions">
      <ng-content />
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 20px;
    }
    h1 {
      margin: 0;
      font-size: 22px;
      font-weight: 600;
    }
    p {
      margin: 4px 0 0;
      color: var(--wf-text-muted);
    }
    .actions {
      display: flex;
      gap: 8px;
    }
  `,
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly subtitle = input<string>();
}
