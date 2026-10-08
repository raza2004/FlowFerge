import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { AdminService } from '../../shared/services/admin.service';
import { AdminTenantDto, FeatureFlagDto } from '../../shared/models/admin.models';

@Component({
  selector: 'app-features',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatSlideToggleModule],
  template: `
    <div class="max-w-5xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Feature flags</h1>
      <p class="text-zinc-500 mb-8">
        Switch features on or off for everyone, or override a single workspace. A workspace override always wins over the global setting.
      </p>

      @if (error()) {
        <div class="mb-5 rounded-xl border border-red-200 bg-red-50 p-3 flex items-start gap-2 text-sm text-red-700">
          <span class="flex-1">{{ error() }}</span>
          <button class="text-red-400 hover:text-red-600" (click)="error.set(null)"><mat-icon class="!text-base">close</mat-icon></button>
        </div>
      }

      @if (loading()) {
        <div class="space-y-4">
          @for (i of [1, 2, 3]; track i) { <div class="h-28 rounded-xl bg-zinc-100 animate-pulse"></div> }
        </div>
      } @else {
        <div class="space-y-4">
          @for (f of flags(); track f.key) {
            <section class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
              <div class="p-5 flex items-start gap-4">
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <h2 class="font-semibold text-zinc-900">{{ f.name }}</h2>
                    <span class="font-mono text-xs text-zinc-400">{{ f.key }}</span>
                  </div>
                  <p class="text-sm text-zinc-500 mt-1">{{ f.description }}</p>
                </div>
                <div class="flex items-center gap-2 flex-shrink-0">
                  <span class="text-xs" [class]="f.isEnabled ? 'text-accent-700' : 'text-zinc-400'">{{ f.isEnabled ? 'On for everyone' : 'Off globally' }}</span>
                  <mat-slide-toggle [checked]="f.isEnabled" [disabled]="busyKey() === f.key"
                                    [attr.aria-label]="'Turn ' + f.name + ' ' + (f.isEnabled ? 'off' : 'on') + ' globally'"
                                    (change)="setGlobal(f, $event.checked)" />
                </div>
              </div>

              <div class="border-t border-zinc-100 bg-zinc-50/60 px-5 py-3">
                <div class="text-xs font-medium uppercase tracking-wide text-zinc-500 mb-2">Workspace overrides</div>

                @for (o of f.overrides; track o.tenantId) {
                  <div class="flex items-center gap-3 py-1.5 text-sm">
                    <span class="flex-1 text-zinc-800 truncate">{{ o.tenantName }}</span>
                    <span class="text-xs px-2 py-0.5 rounded-full" [class]="o.isEnabled ? 'bg-accent-50 text-accent-700' : 'bg-red-50 text-red-700'">
                      Forced {{ o.isEnabled ? 'on' : 'off' }}
                    </span>
                    <button class="text-xs text-zinc-500 hover:text-zinc-900" (click)="setOverride(f, o.tenantId, !o.isEnabled)">
                      Switch {{ o.isEnabled ? 'off' : 'on' }}
                    </button>
                    <button class="text-xs text-zinc-500 hover:text-red-600" (click)="setOverride(f, o.tenantId, null)">Remove</button>
                  </div>
                } @empty {
                  <p class="text-sm text-zinc-400 py-1">None. Every workspace follows the global setting.</p>
                }

                <div class="flex items-center gap-2 mt-2">
                  <select #tenant class="text-sm border border-zinc-200 rounded-md px-2 py-1.5 bg-white focus:border-admin-500 focus:outline-none max-w-[260px]"
                          aria-label="Workspace to override">
                    <option value="">Choose a workspace...</option>
                    @for (t of tenantsWithoutOverride(f); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
                  </select>
                  <button class="text-sm px-3 py-1.5 rounded-md border border-zinc-200 bg-white hover:bg-zinc-50 disabled:opacity-40"
                          [disabled]="!tenant.value" (click)="addOverride(f, tenant, !f.isEnabled)">
                    Force {{ f.isEnabled ? 'off' : 'on' }}
                  </button>
                </div>
              </div>
            </section>
          }
        </div>
      }
    </div>
  `
})
export class FeaturesComponent implements OnInit {
  private admin = inject(AdminService);

  flags = signal<FeatureFlagDto[]>([]);
  tenants = signal<AdminTenantDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  busyKey = signal<string | null>(null);

  ngOnInit() {
    this.admin.getTenants().subscribe(t => this.tenants.set(t));
    this.reload();
  }

  private reload() {
    this.admin.getFeatureFlags().subscribe({
      next: flags => {
        this.flags.set(flags);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load feature flags.');
        this.loading.set(false);
      }
    });
  }

  private fail(err: HttpErrorResponse, fallback: string) {
    this.error.set(err.error?.detail ?? fallback);
    this.busyKey.set(null);
    this.reload();
  }

  tenantsWithoutOverride(flag: FeatureFlagDto): AdminTenantDto[] {
    const taken = new Set(flag.overrides.map(o => o.tenantId));
    return this.tenants().filter(t => !taken.has(t.id));
  }

  setGlobal(flag: FeatureFlagDto, enabled: boolean) {
    this.busyKey.set(flag.key);
    this.admin.setFeatureEnabled(flag.key, enabled).subscribe({
      next: () => {
        this.flags.update(list => list.map(f => f.key === flag.key ? { ...f, isEnabled: enabled } : f));
        this.busyKey.set(null);
      },
      error: err => this.fail(err, 'Could not change that feature.')
    });
  }

  addOverride(flag: FeatureFlagDto, select: HTMLSelectElement, enabled: boolean) {
    const tenantId = select.value;
    if (!tenantId) return;
    select.value = '';
    this.setOverride(flag, tenantId, enabled);
  }

  setOverride(flag: FeatureFlagDto, tenantId: string, enabled: boolean | null) {
    this.admin.setFeatureOverride(flag.key, tenantId, enabled).subscribe({
      next: () => this.reload(),
      error: err => this.fail(err, 'Could not change that override.')
    });
  }
}
