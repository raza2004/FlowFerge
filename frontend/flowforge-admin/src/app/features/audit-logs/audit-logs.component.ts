import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { AdminService } from '../../shared/services/admin.service';
import { AdminAuditLogDto } from '../../shared/models/admin.models';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule, DatePipe, MatIconModule],
  template: `
    <div class="max-w-6xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Audit logs</h1>
      <p class="text-zinc-500 mb-8">Every mutating action across every workspace, most recent first</p>

      @if (logs().length === 0) {
        <div class="bg-white rounded-xl border border-zinc-200 p-16 text-center">
          <mat-icon class="!text-6xl !w-16 !h-16 text-zinc-300">history</mat-icon>
          <h3 class="font-display text-lg mt-4 text-zinc-900">No activity yet</h3>
          <p class="text-zinc-500 mt-1 text-sm">Actions taken across workspaces will show up here.</p>
        </div>
      } @else {
        <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-zinc-50 text-zinc-500 text-xs uppercase tracking-wide">
              <tr>
                <th class="text-left px-4 py-2.5 font-medium">When</th>
                <th class="text-left px-4 py-2.5 font-medium">Workspace</th>
                <th class="text-left px-4 py-2.5 font-medium">Actor</th>
                <th class="text-left px-4 py-2.5 font-medium">Action</th>
                <th class="text-left px-4 py-2.5 font-medium">Context</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-zinc-100">
              @for (log of logs(); track log.id) {
                <tr class="hover:bg-zinc-50">
                  <td class="px-4 py-2.5 text-zinc-500 whitespace-nowrap">{{ log.createdAt | date:'MMM d, h:mm a' }}</td>
                  <td class="px-4 py-2.5 text-zinc-900">{{ log.tenantName ?? '—' }}</td>
                  <td class="px-4 py-2.5 text-zinc-600">{{ log.userName ?? 'System' }}</td>
                  <td class="px-4 py-2.5 font-mono text-xs text-admin-900">{{ log.action }}</td>
                  <td class="px-4 py-2.5 text-zinc-500">{{ log.entityType }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `
})
export class AuditLogsComponent implements OnInit {
  private admin = inject(AdminService);
  logs = signal<AdminAuditLogDto[]>([]);

  ngOnInit() {
    this.admin.getAuditLogs().subscribe(l => this.logs.set(l));
  }
}
