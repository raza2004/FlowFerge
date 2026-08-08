import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="max-w-3xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Settings</h1>
      <p class="text-zinc-500 mb-8">Manage your workspace and account</p>

      <div class="space-y-4">
        <div class="bg-white rounded-xl border border-zinc-200 p-6">
          <h2 class="font-display font-semibold text-zinc-900 mb-4">Workspace</h2>
          <div class="grid grid-cols-2 gap-4 text-sm">
            <div>
              <div class="text-zinc-500 text-xs mb-1">Name</div>
              <div class="text-zinc-900">{{ auth.tenant()?.name }}</div>
            </div>
            <div>
              <div class="text-zinc-500 text-xs mb-1">URL slug</div>
              <div class="text-zinc-900 font-mono">{{ auth.tenant()?.slug }}</div>
            </div>
            <div>
              <div class="text-zinc-500 text-xs mb-1">Plan</div>
              <div class="text-zinc-900">{{ auth.tenant()?.planTier }}</div>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-xl border border-zinc-200 p-6">
          <h2 class="font-display font-semibold text-zinc-900 mb-4">Account</h2>
          <div class="grid grid-cols-2 gap-4 text-sm">
            <div>
              <div class="text-zinc-500 text-xs mb-1">Name</div>
              <div class="text-zinc-900">{{ auth.user()?.fullName }}</div>
            </div>
            <div>
              <div class="text-zinc-500 text-xs mb-1">Email</div>
              <div class="text-zinc-900">{{ auth.user()?.email }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SettingsComponent {
  auth = inject(AuthService);
}
