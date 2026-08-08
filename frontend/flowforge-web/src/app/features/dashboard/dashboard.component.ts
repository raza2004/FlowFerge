import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { DashboardService } from '../../shared/services/dashboard.service';
import { ProjectsService } from '../../shared/services/projects.service';
import { DashboardStatsDto, MyTaskDto, ProjectSummaryDto } from '../../shared/models/project.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, MatButtonModule],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private projectsService  = inject(ProjectsService);

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
    }[priority] ?? 'text-gray-400';
  }

  priorityIcon(priority: string): string {
    return {
      Critical: 'crisis_alert', Highest: 'crisis_alert',
      High: 'arrow_upward', Medium: 'remove',
      Low: 'arrow_downward', Lowest: 'arrow_downward'
    }[priority] ?? 'remove';
  }
}
