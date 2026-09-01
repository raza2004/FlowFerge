import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { SettingsService } from '../../shared/services/settings.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, MatSlideToggleModule, MatButtonModule, MatIconModule],
  template: `
    <div class="max-w-3xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Settings</h1>
      <p class="text-zinc-500 mb-8">Manage your workspace and account</p>

      <div class="space-y-4">
        <div class="bg-white rounded-xl border border-zinc-200 p-6">
          <h2 class="font-display font-semibold text-zinc-900 mb-4">Workspace</h2>
          <div class="grid grid-cols-2 gap-4 text-sm mb-5">
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

          <div class="border-t border-zinc-100 pt-4">
            <div class="text-sm font-medium text-zinc-900 mb-1">Slack notifications</div>
            <p class="text-xs text-zinc-500 mb-3">
              Paste an
              <a href="https://api.slack.com/messaging/webhooks" target="_blank" class="text-forge-600 hover:underline">incoming webhook URL</a>
              to also post workspace alerts (like automation triggers) into a Slack channel.
              Only workspace owners and admins can change this.
            </p>
            <div class="flex items-center gap-2">
              <input type="text" [(ngModel)]="slackWebhookInput" placeholder="https://hooks.slack.com/services/…"
                     class="flex-1 text-sm border border-zinc-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-forge-400">
              <button mat-stroked-button [disabled]="slackSaving()" (click)="saveSlackWebhook()">Save</button>
              @if (auth.tenant()?.slackWebhookUrl) {
                <button mat-icon-button matTooltip="Remove" [disabled]="slackSaving()" (click)="clearSlackWebhook()">
                  <mat-icon class="!text-base text-zinc-400">close</mat-icon>
                </button>
              }
            </div>
            @if (slackError()) { <p class="text-xs text-red-600 mt-1.5">{{ slackError() }}</p> }
            @if (slackSaved()) { <p class="text-xs text-green-700 mt-1.5">Saved.</p> }
          </div>
        </div>

        <div class="bg-white rounded-xl border border-zinc-200 p-6">
          <h2 class="font-display font-semibold text-zinc-900 mb-4">Account</h2>
          <div class="grid grid-cols-2 gap-4 text-sm mb-5">
            <div>
              <div class="text-zinc-500 text-xs mb-1">Name</div>
              <div class="text-zinc-900">{{ auth.user()?.fullName }}</div>
            </div>
            <div>
              <div class="text-zinc-500 text-xs mb-1">Email</div>
              <div class="text-zinc-900">{{ auth.user()?.email }}</div>
            </div>
          </div>

          <div class="border-t border-zinc-100 pt-4 flex items-center justify-between">
            <div>
              <div class="text-sm font-medium text-zinc-900">Email notifications</div>
              <p class="text-xs text-zinc-500">Get emailed for the same things you'd see in your Inbox.</p>
            </div>
            <mat-slide-toggle [checked]="auth.user()?.emailNotificationsEnabled ?? true"
                               [disabled]="emailPrefSaving()"
                               (change)="toggleEmailNotifications($event.checked)">
            </mat-slide-toggle>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SettingsComponent {
  auth = inject(AuthService);
  private settingsService = inject(SettingsService);

  slackWebhookInput = this.auth.tenant()?.slackWebhookUrl ?? '';
  slackSaving = signal(false);
  slackError = signal<string | null>(null);
  slackSaved = signal(false);

  emailPrefSaving = signal(false);

  saveSlackWebhook() {
    this.commitSlackWebhook(this.slackWebhookInput.trim() || null);
  }

  clearSlackWebhook() {
    this.slackWebhookInput = '';
    this.commitSlackWebhook(null);
  }

  private commitSlackWebhook(webhookUrl: string | null) {
    this.slackSaving.set(true);
    this.slackError.set(null);
    this.slackSaved.set(false);
    this.settingsService.updateSlackWebhook(webhookUrl).subscribe({
      next: () => {
        this.auth.patchTenant({ slackWebhookUrl: webhookUrl });
        this.slackSaving.set(false);
        this.slackSaved.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.slackError.set(err.error?.detail ?? 'Could not save the Slack webhook.');
        this.slackSaving.set(false);
      }
    });
  }

  toggleEmailNotifications(enabled: boolean) {
    this.emailPrefSaving.set(true);
    this.settingsService.updateNotificationPreferences(enabled).subscribe({
      next: () => {
        this.auth.patchUser({ emailNotificationsEnabled: enabled });
        this.emailPrefSaving.set(false);
      },
      error: () => this.emailPrefSaving.set(false)
    });
  }
}
