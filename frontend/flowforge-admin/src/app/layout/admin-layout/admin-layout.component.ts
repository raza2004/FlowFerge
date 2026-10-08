import { Component, inject, AfterViewInit, ViewChildren, ViewChild, QueryList, ElementRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminAuthService } from '../../core/services/admin-auth.service';

interface NavIndicator {
  top: number;
  height: number;
  visible: boolean;
}

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatButtonModule, MatTooltipModule],
  templateUrl: './admin-layout.component.html'
})
export class AdminLayoutComponent implements AfterViewInit {
  auth = inject(AdminAuthService);
  private router = inject(Router);

  @ViewChildren('navItem') private navItems!: QueryList<ElementRef<HTMLElement>>;
  @ViewChild('routeContainer') private routeContainer!: ElementRef<HTMLElement>;
  indicator = signal<NavIndicator>({ top: 0, height: 0, visible: false });

  nav = [
    { path: '/dashboard',   label: 'Dashboard',   icon: 'space_dashboard' },
    { path: '/tenants',     label: 'Tenants',     icon: 'apartment' },
    { path: '/users',       label: 'Users',       icon: 'group' },
    { path: '/features',    label: 'Feature flags', icon: 'toggle_on' },
    { path: '/audit-logs',  label: 'Audit logs',  icon: 'history' }
  ];

  ngAfterViewInit() {
    queueMicrotask(() => this.sync());
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      setTimeout(() => this.sync());
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

  onActive(isActive: boolean, index: number) {
    if (isActive) queueMicrotask(() => this.measure(index));
  }

  private sync() {
    const index = this.nav.findIndex(i => this.router.url.startsWith(i.path));
    if (index >= 0) this.measure(index);
  }

  private measure(index: number) {
    const el = this.navItems?.toArray()[index]?.nativeElement;
    if (el) this.indicator.set({ top: el.offsetTop, height: el.offsetHeight, visible: true });
  }

  logout() { this.auth.logout(); }
}
