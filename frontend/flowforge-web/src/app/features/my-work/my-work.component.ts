import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { DashboardService } from '../../shared/services/dashboard.service';
import { MyTaskDto } from '../../shared/models/project.models';

@Component({
  selector: 'app-my-work',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto px-8 py-10">
      <div class="mb-8">
        <h1 class="text-3xl font-display font-semibold text-zinc-900">My work</h1>
        <p class="text-zinc-500 mt-1">All tasks assigned to you across projects</p>
      </div>

      <div class="bg-white rounded-xl border border-zinc-200 overflow-hidden">
        @if (tasks().length === 0) {
          <div class="p-16 text-center">
            <mat-icon class="!text-6xl !w-16 !h-16 text-zinc-300">task_alt</mat-icon>
            <h3 class="font-display text-lg mt-4 text-zinc-900">All clear</h3>
            <p class="text-zinc-500 mt-1 text-sm">No tasks assigned to you yet</p>
          </div>
        } @else {
          <div class="divide-y divide-zinc-100">
            @for (task of tasks(); track task.id) {
              <div class="flex items-center gap-4 px-6 py-4 hover:bg-zinc-50">
                <div class="w-2 h-2 rounded-full" [style.background]="task.projectColor"></div>
                <span class="text-xs font-mono text-zinc-500 w-20">{{ task.taskNumber }}</span>
                <span class="text-sm text-zinc-900 flex-1">{{ task.title }}</span>
                <span class="text-xs text-zinc-500">{{ task.projectName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-medium" [class]="priorityClass(task.priority)">
                  {{ task.priority }}
                </span>
                @if (task.dueDate) {
                  <span class="text-xs w-20 text-right" [class.text-red-600]="task.isOverdue" [class.text-zinc-500]="!task.isOverdue">
                    {{ task.dueDate | date:'MMM d' }}
                  </span>
                }
              </div>
            }
          </div>
        }
      </div>
    </div>
  `
})
export class MyWorkComponent implements OnInit {
  private service = inject(DashboardService);
  tasks = signal<MyTaskDto[]>([]);

  ngOnInit() {
    this.service.getMyTasks().subscribe(t => this.tasks.set(t));
  }

  priorityClass(p: string): string {
    return {
      Critical: 'text-red-600 bg-red-50',
      Highest: 'text-red-600 bg-red-50',
      High: 'text-orange-600 bg-orange-50',
      Medium: 'text-amber-600 bg-amber-50',
      Low: 'text-blue-600 bg-blue-50'
    }[p] ?? 'text-zinc-600 bg-zinc-100';
  }
}
