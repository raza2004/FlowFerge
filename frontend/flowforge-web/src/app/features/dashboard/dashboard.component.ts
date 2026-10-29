import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { DashboardService } from '../../shared/services/dashboard.service';
import { ProjectsService } from '../../shared/services/projects.service';
import { DashboardStatsDto, MyTaskDto, ProjectSummaryDto } from '../../shared/models/project.models';
import { AuthService } from '../../core/services/auth.service';
import { CountUpDirective } from '../../shared/directives/count-up.directive';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, MatButtonModule, CountUpDirective],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private projectsService  = inject(ProjectsService);
  private auth = inject(AuthService);

  /** Time of day greeting using the person's first name. */
  greeting(): string {
    const hour = new Date().getHours();
    const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
    const name = this.auth.user()?.firstName;
    return name ? `${part}, ${name}.` : `${part}.`;
  }

  stats    = signal<DashboardStatsDto | null>(null);
  tasks    = signal<MyTaskDto[]>([]);
  projects = signal<ProjectSummaryDto[]>([]);
  loading  = signal(true);

  ngOnInit() {
    this.dashboardService.getStats().subscribe(s => this.stats.set(s));
    this.dashboardService.getMyTasks().subscribe(t => this.tasks.set(t));
    this.projectsService.getAll().subscribe(p => {
      this.projects.set(p);
      this.loading.set(false);
    });
  }

  priorityColor(priority: string): string {
    return {
      Critical: 'text-red-500', Highest: 'text-red-500',
      High: 'text-orange-500', Medium: 'text-yellow-500',
      Low: 'text-blue-400', Lowest: 'text-blue-400'
    }[priority] ?? 'text-ink-faint';
  }

  priorityIcon(priority: string): string {
    return {
      Critical: 'crisis_alert', Highest: 'crisis_alert',
      High: 'arrow_upward', Medium: 'remove',
      Low: 'arrow_downward', Lowest: 'arrow_downward'
    }[priority] ?? 'remove';
  }
}
