import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AdminService } from '../../shared/services/admin.service';
import { AdminUserDto } from '../../shared/models/admin.models';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, DatePipe, MatIconModule, MatButtonModule],
  template: `
    <div class="max-w-6xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Users</h1>
      <p class="text-zinc-500 mb-8">{{ users().length }} accounts across the system</p>

      <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-zinc-50 text-zinc-500 text-xs uppercase tracking-wide">
            <tr>
              <th class="text-left px-4 py-2.5 font-medium">User</th>
              <th class="text-left px-4 py-2.5 font-medium">Role</th>
              <th class="text-left px-4 py-2.5 font-medium">Joined</th>
              <th class="text-left px-4 py-2.5 font-medium">Last login</th>
              <th class="text-left px-4 py-2.5 font-medium">Status</th>
              <th class="px-4 py-2.5"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-zinc-100">
            @for (u of users(); track u.id) {
              <tr class="hover:bg-zinc-50">
                <td class="px-4 py-3">
                  <div class="font-medium text-zinc-900">{{ u.fullName }}</div>
                  <div class="text-xs text-zinc-400">{{ u.email }}</div>
                </td>
                <td class="px-4 py-3">
                  @if (u.isSystemAdmin) {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-admin-100 text-admin-900 flex items-center gap-1 w-fit">
                      <mat-icon class="!text-sm !w-3.5 !h-3.5">shield</mat-icon> Admin
                    </span>
                  } @else {
                    <span class="text-xs text-zinc-500">Member</span>
                  }
                </td>
                <td class="px-4 py-3 text-zinc-600">{{ u.createdAt | date:'MMM d, y' }}</td>
                <td class="px-4 py-3 text-zinc-600">{{ u.lastLoginAt ? (u.lastLoginAt | date:'MMM d, y') : '—' }}</td>
                <td class="px-4 py-3">
                  @if (u.status === 'Suspended') {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-red-100 text-red-700">Suspended</span>
                  } @else {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700">{{ u.status }}</span>
                  }
                </td>
                <td class="px-4 py-3 text-right">
                  @if (u.status === 'Suspended') {
                    <button mat-button class="!text-xs" (click)="reactivate(u)">Reactivate</button>
                  } @else {
                    <button mat-button color="warn" class="!text-xs" (click)="suspend(u)">Suspend</button>
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
export class UsersComponent implements OnInit {
  private admin = inject(AdminService);
  users = signal<AdminUserDto[]>([]);

  ngOnInit() {
    this.load();
  }

  private load() {
    this.admin.getUsers().subscribe(u => this.users.set(u));
  }

  suspend(user: AdminUserDto) {
    this.admin.suspendUser(user.id).subscribe(() => this.load());
  }

  reactivate(user: AdminUserDto) {
    this.admin.reactivateUser(user.id).subscribe(() => this.load());
  }
}
