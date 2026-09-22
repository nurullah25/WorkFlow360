import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { NAV_SECTIONS } from './nav-items';
import { NotificationBell } from './notification-bell';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDividerModule,
    NotificationBell,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);

  protected readonly isMobile = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 959px)')
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  protected readonly sections = computed(() =>
    NAV_SECTIONS.map((section) => ({
      ...section,
      items: section.items.filter((item) => !item.roles || this.auth.hasRole(...item.roles)),
    })).filter((section) => section.items.length > 0),
  );

  protected readonly initials = computed(() => {
    const name = this.auth.user()?.fullName ?? '';
    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join('');
  });

  protected closeOnMobile(sidenav: MatSidenav): void {
    if (this.isMobile()) {
      sidenav.close();
    }
  }
}
