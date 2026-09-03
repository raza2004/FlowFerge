import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminService } from '../../shared/services/admin.service';
import { AdminTenantDto } from '../../shared/models/admin.models';
import { SuspendTenantDialogComponent } from './suspend-tenant-dialog.component';

@Component({
  selector: 'app-tenants',
  standalone: true,
  imports: [CommonModule, DatePipe, MatIconModule, MatButtonModule, MatDialogModule, MatTooltipModule],
  template: `
    <div class="max-w-6xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Tenants</h1>
      <p class="text-zinc-500 mb-8">{{ tenants().length }} workspaces across the system</p>

      <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-zinc-50 text-zinc-500 text-xs uppercase tracking-wide">
            <tr>
              <th class="text-left px-4 py-2.5 font-medium">Workspace</th>
              <th class="text-left px-4 py-2.5 font-medium">Plan</th>
              <th class="text-left px-4 py-2.5 font-medium">Members</th>
              <th class="text-left px-4 py-2.5 font-medium">Projects</th>
              <th class="text-left px-4 py-2.5 font-medium">Created</th>
              <th class="text-left px-4 py-2.5 font-medium">Status</th>
              <th class="px-4 py-2.5"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-zinc-100">
            @for (t of tenants(); track t.id) {
              <tr class="hover:bg-zinc-50">
                <td class="px-4 py-3">
                  <div class="font-medium text-zinc-900">{{ t.name }}</div>
                  <div class="text-xs text-zinc-400 font-mono">{{ t.slug }}</div>
                </td>
                <td class="px-4 py-3 text-zinc-600">{{ t.planTier }}</td>
                <td class="px-4 py-3 text-zinc-600">{{ t.memberCount }}</td>
                <td class="px-4 py-3 text-zinc-600">{{ t.projectCount }}</td>
                <td class="px-4 py-3 text-zinc-600">{{ t.createdAt | date:'MMM d, y' }}</td>
                <td class="px-4 py-3">
                  @if (t.isActive) {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700">Active</span>
                  } @else {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-red-100 text-red-700"
                          [matTooltip]="t.suspensionReason ?? ''">Suspended</span>
                  }
                </td>
                <td class="px-4 py-3 text-right">
                  @if (t.isActive) {
                    <button mat-button color="warn" class="!text-xs" (click)="suspend(t)">Suspend</button>
                  } @else {
                    <button mat-button class="!text-xs" (click)="reactivate(t)">Reactivate</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class TenantsComponent implements OnInit {
  private admin = inject(AdminService);
  private dialog = inject(MatDialog);

  tenants = signal<AdminTenantDto[]>([]);

  ngOnInit() {
    this.load();
  }

  private load() {
    this.admin.getTenants().subscribe(t => this.tenants.set(t));
  }

  suspend(tenant: AdminTenantDto) {
    const ref = this.dialog.open(SuspendTenantDialogComponent, { width: '420px' });
    ref.afterClosed().subscribe((reason: string | undefined) => {
      if (!reason) return;
      this.admin.suspendTenant(tenant.id, reason).subscribe(() => this.load());
    });
  }

  reactivate(tenant: AdminTenantDto) {
    this.admin.reactivateTenant(tenant.id).subscribe(() => this.load());
  }
}
