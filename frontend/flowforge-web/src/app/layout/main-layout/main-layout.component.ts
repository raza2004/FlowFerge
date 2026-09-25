import { Component, inject, OnInit, OnDestroy, AfterViewInit, ViewChildren, ViewChild, QueryList, ElementRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';
import { NotificationsRealtimeService } from '../../core/services/notifications-realtime.service';
import { NotificationsService } from '../../shared/services/notifications.service';

interface NavIndicator {
  top: number;
  height: number;
  visible: boolean;
}

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatIconModule, MatMenuModule, MatButtonModule
  ],
  templateUrl: './main-layout.component.html'
})
export class MainLayoutComponent implements OnInit, AfterViewInit, OnDestroy {
  auth = inject(AuthService);
  private notificationsRealtime = inject(NotificationsRealtimeService);
  private router = inject(Router);
  notifications = inject(NotificationsService);

  @ViewChildren('primaryItem') private primaryItems!: QueryList<ElementRef<HTMLElement>>;
  @ViewChildren('workspaceItem') private workspaceItems!: QueryList<ElementRef<HTMLElement>>;
  @ViewChild('routeContainer') private routeContainer!: ElementRef<HTMLElement>;

  primaryIndicator = signal<NavIndicator>({ top: 0, height: 0, visible: false });
  workspaceIndicator = signal<NavIndicator>({ top: 0, height: 0, visible: false });
  badgeBump = signal(false);

  primaryNav = [
    { path: '/inbox',     label: 'Inbox',     icon: 'inbox' },
    { path: '/my-work',   label: 'My Work',   icon: 'check_circle' },
    { path: '/dashboard', label: 'Dashboard', icon: 'space_dashboard' }
  ];

  workspaceNav = [
    { path: '/projects', label: 'Projects', icon: 'folder' },
    { path: '/team',     label: 'Team',     icon: 'group' },
    { path: '/settings', label: 'Settings', icon: 'settings' }
  ];

  get initials(): string {
    const u = this.auth.user();
    return u ? `${u.firstName[0]}${u.lastName[0]}`.toUpperCase() : '';
  }

  get workspaceInitial(): string {
    return this.auth.tenant()?.name?.[0]?.toUpperCase() ?? 'W';
  }

  async ngOnInit() {
    this.notifications.refreshUnreadCount();

    await this.notificationsRealtime.startConnection();
    this.notificationsRealtime.notificationReceived$.subscribe(() => {
      this.notifications.unreadCount.update(count => count + 1);
      this.badgeBump.set(true);
      setTimeout(() => this.badgeBump.set(false), 600);
    });
  }

  ngAfterViewInit() {
    queueMicrotask(() => this.syncIndicators());
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      setTimeout(() => this.syncIndicators());
      this.replayRouteFade();
    });
  }

  private replayRouteFade() {
    const el = this.routeContainer?.nativeElement;
    if (!el) return;
    el.classList.remove('route-fade-play');
    void el.offsetWidth;
    el.classList.add('route-fade-play');
  }

  async ngOnDestroy() {
    await this.notificationsRealtime.stop();
  }

  onPrimaryActive(isActive: boolean, index: number) {
    if (isActive) queueMicrotask(() => this.measure(this.primaryItems, index, this.primaryIndicator));
  }

  onWorkspaceActive(isActive: boolean, index: number) {
    if (isActive) queueMicrotask(() => this.measure(this.workspaceItems, index, this.workspaceIndicator));
  }

  private syncIndicators() {
    const primaryIndex = this.primaryNav.findIndex(i => this.router.url.startsWith(i.path));
    if (primaryIndex >= 0) this.measure(this.primaryItems, primaryIndex, this.primaryIndicator);

    const workspaceIndex = this.workspaceNav.findIndex(i => this.router.url.startsWith(i.path));
    if (workspaceIndex >= 0) this.measure(this.workspaceItems, workspaceIndex, this.workspaceIndicator);
  }

  private measure(items: QueryList<ElementRef<HTMLElement>> | undefined, index: number, target: ReturnType<typeof signal<NavIndicator>>) {
    const el = items?.toArray()[index]?.nativeElement;
    if (el) target.set({ top: el.offsetTop, height: el.offsetHeight, visible: true });
  }

  logout() { this.auth.logout(); }
}
