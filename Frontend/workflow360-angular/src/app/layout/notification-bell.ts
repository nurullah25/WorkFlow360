import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink } from '@angular/router';

import { AppNotification, NotificationService } from '../features/notifications/notification.service';

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe, RouterLink, MatBadgeModule, MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="menu"
      (menuOpened)="loadLatest()"
      [attr.aria-label]="'Notifications, ' + notifications.unreadCount() + ' unread'"
    >
      <mat-icon
        [matBadge]="notifications.unreadCount()"
        [matBadgeHidden]="notifications.unreadCount() === 0"
        matBadgeSize="small"
        matBadgeColor="warn"
        aria-hidden="false"
      >
        notifications
      </mat-icon>
    </button>

    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      <div class="header" (click)="$event.stopPropagation()">
        <strong>Notifications</strong>
        @if (notifications.unreadCount() > 0) {
          <button mat-button (click)="markAllRead()">Mark all as read</button>
        }
      </div>
      <mat-divider />

      @for (item of latest(); track item.id) {
        <button mat-menu-item class="item" [class.unread]="!item.isRead" (click)="open(item)">
          <div class="item-title">{{ item.title }}</div>
          <div class="item-message">{{ item.message }}</div>
          <div class="item-time">{{ item.createdAt | date: 'd MMM, HH:mm' }}</div>
        </button>
      } @empty {
        <div class="empty">{{ loading() ? 'Loading…' : "You're all caught up." }}</div>
      }

      <mat-divider />
      <a mat-menu-item routerLink="/notifications" class="view-all">View all notifications</a>
    </mat-menu>
  `,
  styles: `
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 4px 8px 4px 16px;
    }
    .item {
      height: auto !important;
      padding-top: 8px !important;
      padding-bottom: 8px !important;
      line-height: 1.35;
      white-space: normal;
      border-left: 3px solid transparent;
    }
    .item.unread {
      border-left-color: var(--mat-sys-primary);
      background: #f5f8fd;
    }
    .item-title {
      font-weight: 600;
    }
    .item-message {
      font-size: 13px;
      color: var(--wf-text-muted);
    }
    .item-time {
      font-size: 11px;
      color: var(--wf-text-muted);
      margin-top: 2px;
    }
    .empty {
      padding: 16px;
      color: var(--wf-text-muted);
    }
    .view-all {
      justify-content: center;
      color: var(--mat-sys-primary);
    }
  `,
})
export class NotificationBell {
  protected readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly latest = signal<AppNotification[]>([]);
  protected readonly loading = signal(false);

  constructor() {
    this.notifications.pollUnreadCount().pipe(takeUntilDestroyed()).subscribe();
  }

  protected loadLatest(): void {
    this.notifications.refreshUnreadCount();
    this.loading.set(true);
    this.notifications.list(1, 8).subscribe({
      next: (page) => {
        this.latest.set(page.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
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
    this.notifications.markAllRead().subscribe(() => {
      this.latest.update((items) => items.map((item) => ({ ...item, isRead: true })));
    });
  }
}
