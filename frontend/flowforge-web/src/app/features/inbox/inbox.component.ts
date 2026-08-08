import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="max-w-4xl mx-auto px-8 py-10">
      <h1 class="text-3xl font-display font-semibold text-zinc-900 mb-2">Inbox</h1>
      <p class="text-zinc-500 mb-8">Notifications and mentions across your workspace</p>

      <div class="bg-white rounded-xl border border-zinc-200 p-16 text-center">
        <mat-icon class="!text-6xl !w-16 !h-16 text-zinc-300">inbox</mat-icon>
        <h3 class="font-display text-lg mt-4 text-zinc-900">Your inbox is empty</h3>
        <p class="text-zinc-500 mt-1 text-sm max-w-md mx-auto">
          When teammates @mention you or update tasks you're watching, you'll see them here.
        </p>
      </div>
    </div>
  `
})
export class InboxComponent {}
