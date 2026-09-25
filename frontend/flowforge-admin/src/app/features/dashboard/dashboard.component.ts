import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { AdminService } from '../../shared/services/admin.service';
import { AdminStatsDto } from '../../shared/models/admin.models';
import { CountUpDirective } from '../../shared/directives/count-up.directive';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, MatIconModule, CountUpDirective],
  template: `
    <div class="max-w-5xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">System dashboard</h1>
      <p class="text-zinc-500 mb-8">Everything running on FlowForge, across every workspace</p>

      @if (stats(); as s) {
        <div class="grid grid-cols-4 gap-4">
          <div class="bg-white rounded-xl border border-zinc-200 p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-300">
            <div class="flex items-center gap-2 text-zinc-500 text-xs uppercase tracking-wide mb-2">
              <mat-icon class="!text-base">apartment</mat-icon> Tenants
            </div>
            <div class="text-3xl font-bold text-zinc-900" [appCountUp]="s.totalTenants"></div>
            <div class="text-xs text-green-600 mt-1">{{ s.activeTenants }} active</div>
          </div>
          <div class="bg-white rounded-xl border border-zinc-200 p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-300">
            <div class="flex items-center gap-2 text-zinc-500 text-xs uppercase tracking-wide mb-2">
              <mat-icon class="!text-base">group</mat-icon> Users
            </div>
            <div class="text-3xl font-bold text-zinc-900" [appCountUp]="s.totalUsers"></div>
          </div>
          <div class="bg-white rounded-xl border border-zinc-200 p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-300">
            <div class="flex items-center gap-2 text-zinc-500 text-xs uppercase tracking-wide mb-2">
              <mat-icon class="!text-base">folder</mat-icon> Projects
            </div>
            <div class="text-3xl font-bold text-zinc-900" [appCountUp]="s.totalProjects"></div>
          </div>
          <div class="bg-white rounded-xl border border-zinc-200 p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-300">
            <div class="flex items-center gap-2 text-zinc-500 text-xs uppercase tracking-wide mb-2">
              <mat-icon class="!text-base">pause_circle</mat-icon> Suspended
            </div>
            <div class="text-3xl font-bold text-zinc-900" [appCountUp]="s.totalTenants - s.activeTenants"></div>
          </div>
        </div>
      } @else {
        <div class="grid grid-cols-4 gap-4">
          @for (i of [1,2,3,4]; track i) {
            <div class="bg-white rounded-xl border border-zinc-200 p-5 space-y-3">
              <div class="h-3 w-16 rounded bg-zinc-200 animate-pulse"></div>
              <div class="h-8 w-12 rounded bg-zinc-200 animate-pulse"></div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class DashboardComponent implements OnInit {
  private admin = inject(AdminService);
  stats = signal<AdminStatsDto | null>(null);

  ngOnInit() {
    this.admin.getStats().subscribe(s => this.stats.set(s));
  }
}
