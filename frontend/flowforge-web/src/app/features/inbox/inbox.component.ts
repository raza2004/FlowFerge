import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { NotificationsService } from '../../shared/services/notifications.service';
import { NotificationsRealtimeService } from '../../core/services/notifications-realtime.service';
import { NotificationDto, NotificationType } from '../../shared/models/notification.models';

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [CommonModule, MatIconModule, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Inbox</h1>
      <p class="text-zinc-500 mb-8">Notifications and mentions across your workspace</p>

      @if (notifications().length === 0) {
        <div class="bg-white rounded-xl border border-zinc-200 p-16 text-center">
          <mat-icon class="!text-6xl !w-16 !h-16 text-zinc-300">inbox</mat-icon>
          <h3 class="font-display text-lg mt-4 text-zinc-900">Your inbox is empty</h3>
          <p class="text-zinc-500 mt-1 text-sm max-w-md mx-auto">
            When teammates @mention you, assign you work, or an automation fires,
            you'll see it here in real time.
          </p>
        </div>
      } @else {
        <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
          <div class="divide-y divide-zinc-100">
            @for (n of notifications(); track n.id) {
              <button (click)="markRead(n)"
                      class="w-full flex items-start gap-3 px-6 py-4 text-left hover:bg-zinc-50 transition-colors"
                      [class.bg-forge-50]="!n.isRead">
                <mat-icon class="!text-lg !w-5 !h-5 mt-0.5 flex-shrink-0"
                          [class.text-forge-600]="!n.isRead" [class.text-zinc-300]="n.isRead">
                  {{ icon(n.type) }}
                </mat-icon>
                <div class="flex-1 min-w-0">
                  <div class="text-sm text-zinc-900" [class.font-semibold]="!n.isRead">{{ n.title }}</div>
                  <div class="text-sm text-zinc-500 mt-0.5">{{ n.message }}</div>
                  <div class="text-xs text-zinc-400 mt-1">{{ n.createdAt | date:'MMM d, h:mm a' }}</div>
                </div>
                @if (!n.isRead) {
                  <div class="w-2 h-2 rounded-full bg-forge-500 mt-2 flex-shrink-0"></div>
                }
              </button>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class InboxComponent implements OnInit {
  private service = inject(NotificationsService);
  private realtime = inject(NotificationsRealtimeService);

  notifications = signal<NotificationDto[]>([]);

  ngOnInit() {
    this.service.getMine().subscribe(list => this.notifications.set(list));

    // MainLayoutComponent already owns the hub connection for the session; just listen in.
    this.realtime.notificationReceived$.subscribe(n => {
      this.notifications.update(list => [n, ...list]);
    });
  }

  markRead(n: NotificationDto) {
    if (n.isRead) return;
    this.service.markRead(n.id).subscribe(() => {
      this.notifications.update(list =>
        list.map(x => x.id === n.id ? { ...x, isRead: true } : x));
    });
  }

  icon(type: NotificationType): string {
    return type === NotificationType.TaskAssigned ? 'assignment_ind' : 'bolt';
  }
}
