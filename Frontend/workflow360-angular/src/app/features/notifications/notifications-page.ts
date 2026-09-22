import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router } from '@angular/router';

import { PageHeader } from '../../shared/page-header';
import { emptyPage, PagedResult } from '../../shared/paging';
import { AppNotification, NotificationService } from './notification.service';

@Component({
  selector: 'app-notifications-page',
  imports: [DatePipe, PageHeader, MatButtonModule, MatPaginatorModule, MatSlideToggleModule],
  template: `
    <app-page-header heading="Notifications">
      <button mat-stroked-button [disabled]="notifications.unreadCount() === 0" (click)="markAllRead()">
        Mark all as read
      </button>
    </app-page-header>

    <div class="table-card">
      <div class="toolbar">
        <mat-slide-toggle [checked]="unreadOnly()" (change)="toggleUnread($event.checked)">Unread only</mat-slide-toggle>
      </div>

      <ul class="list">
        @for (item of page().items; track item.id) {
          <li [class.unread]="!item.isRead" [class.clickable]="!!item.linkUrl" (click)="open(item)">
            <div class="title">{{ item.title }}</div>
            <div class="message">{{ item.message }}</div>
            <div class="time">{{ item.createdAt | date: 'medium' }}</div>
          </li>
        } @empty {
          <li class="empty">{{ unreadOnly() ? 'No unread notifications.' : 'No notifications yet.' }}</li>
        }
      </ul>

      <mat-paginator
        [length]="page().totalCount"
        [pageIndex]="pageIndex"
        [pageSize]="pageSize"
        [pageSizeOptions]="[20, 50]"
        (page)="onPage($event)"
      />
    </div>
  `,
  styles: `
    .toolbar {
      padding: 14px 16px;
      border-bottom: 1px solid var(--wf-border);
    }
    .list {
      margin: 0;
      padding: 0;
      list-style: none;

      li {
        padding: 12px 16px;
        border-bottom: 1px solid var(--wf-border);
        border-left: 3px solid transparent;
      }
      li.unread {
        border-left-color: var(--mat-sys-primary);
        background: #f5f8fd;
      }
      li.clickable {
        cursor: pointer;
      }
      li.clickable:hover {
        background: #f0f3f8;
      }
      li.empty {
        padding: 40px 16px;
        text-align: center;
        color: var(--wf-text-muted);
      }
    }
    .title {
      font-weight: 600;
    }
    .message {
      color: var(--wf-text-muted);
    }
    .time {
      margin-top: 2px;
      font-size: 12px;
      color: var(--wf-text-muted);
    }
  `,
})
export class NotificationsPage {
  protected readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly page = signal<PagedResult<AppNotification>>(emptyPage());
  protected readonly unreadOnly = signal(false);
  protected pageIndex = 0;
  protected pageSize = 20;

  constructor() {
    this.load();
  }

  protected toggleUnread(unreadOnly: boolean): void {
    this.unreadOnly.set(unreadOnly);
    this.pageIndex = 0;
    this.load();
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  protected open(item: AppNotification): void {
    if (!item.isRead) {
      this.notifications.markRead(item.id).subscribe();
    }
    if (item.linkUrl) {
      this.router.navigateByUrl(item.linkUrl);
    }
  }

  protected markAllRead(): void {
    this.notifications.markAllRead().subscribe(() => this.load());
  }

  private load(): void {
    this.notifications
      .list(this.pageIndex + 1, this.pageSize, this.unreadOnly())
      .subscribe((page) => this.page.set(page));
  }
}
