import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { DashboardService } from '../../shared/services/dashboard.service';
import { TenantMemberDto } from '../../shared/models/auth.models';

@Component({
  selector: 'app-team',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto px-8 py-10">
      <div class="mb-8">
        <h1 class="text-3xl font-display font-semibold text-zinc-900">Team</h1>
        <p class="text-zinc-500 mt-1">{{ members().length }} members in this workspace</p>
      </div>

      <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
        <div class="divide-y divide-zinc-100">
          @for (m of members(); track m.userId) {
            <div class="flex items-center gap-4 px-6 py-4">
              <div class="w-10 h-10 rounded-full bg-gradient-to-br from-forge-400 to-forge-600 flex items-center justify-center text-zinc-900 font-bold text-sm">
                {{ initials(m.fullName) }}
              </div>
              <div class="flex-1">
                <div class="text-sm font-medium text-zinc-900">{{ m.fullName }}</div>
                <div class="text-xs text-zinc-500">{{ m.email }}</div>
              </div>
              <span class="text-xs px-2.5 py-1 rounded-md bg-zinc-100 text-zinc-700 font-medium">
                {{ m.role }}
              </span>
              <span class="text-xs text-zinc-500">Joined {{ m.joinedAt | date:'MMM y' }}</span>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class TeamComponent implements OnInit {
  private service = inject(DashboardService);
  members = signal<TenantMemberDto[]>([]);

  ngOnInit() {
    this.service.getTeamMembers().subscribe(m => this.members.set(m));
  }

  initials(name: string): string {
    return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
  }
}
