import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpErrorResponse } from '@angular/common/http';
import { AiService } from '../../../shared/services/ai.service';
import { TasksService } from '../../../shared/services/tasks.service';

export interface TaskAiDialogData {
  taskId: string;
  taskTitle: string;
  boardId: string;
}

function aiErrorMessage(err: HttpErrorResponse): string {
  if (err.error?.title === 'AI.NotConfigured') {
    return 'AI features need an OpenAI API key configured on the server (OpenAI:ApiKey).';
  }
  return err.error?.detail ?? 'Something went wrong talking to the AI.';
}

@Component({
  selector: 'app-task-ai-dialog',
  standalone: true,
  imports: [
    CommonModule, MatDialogModule, MatIconModule, MatButtonModule,
    MatCheckboxModule, MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title class="flex items-center gap-2">
      <mat-icon class="text-forge-600">auto_awesome</mat-icon>
      AI Assist — {{ data.taskTitle }}
    </h2>

    <mat-dialog-content class="!min-w-[420px]">
      <div class="space-y-6 pt-1">

        <!-- Subtask breakdown -->
        <div>
          <div class="flex items-center justify-between mb-2">
            <h3 class="text-sm font-semibold text-gray-800">Suggested subtasks</h3>
            <button mat-stroked-button (click)="loadBreakdown()" [disabled]="breakdownLoading()">
              @if (breakdownLoading()) { <mat-spinner diameter="16" class="!inline-block mr-1"></mat-spinner> }
              {{ subtasks().length ? 'Regenerate' : 'Suggest subtasks' }}
            </button>
          </div>

          @if (breakdownError()) {
            <p class="text-xs text-red-600 mb-2">{{ breakdownError() }}</p>
          }

          @if (subtasks().length > 0) {
            <div class="space-y-1 border border-gray-200 rounded-lg p-2">
              @for (title of subtasks(); track title) {
                <label class="flex items-center gap-2 text-sm py-0.5">
                  <mat-checkbox [checked]="selected().has(title)" (change)="toggleSelected(title)"></mat-checkbox>
                  <span>{{ title }}</span>
                </label>
              }
            </div>
            <button mat-raised-button color="primary" class="mt-2 !text-xs"
                    [disabled]="selected().size === 0 || applyingBreakdown()"
                    (click)="applySelected()">
              Add {{ selected().size }} as subtask{{ selected().size === 1 ? '' : 's' }}
            </button>
            @if (appliedCount() !== null) {
              <p class="text-xs text-green-700 mt-1">Added {{ appliedCount() }} subtask(s) to the board.</p>
            }
          }
        </div>

        <!-- Assignee suggestion -->
        <div>
          <div class="flex items-center justify-between mb-2">
            <h3 class="text-sm font-semibold text-gray-800">Suggested assignee</h3>
            <button mat-stroked-button (click)="loadAssignee()" [disabled]="assigneeLoading()">
              @if (assigneeLoading()) { <mat-spinner diameter="16" class="!inline-block mr-1"></mat-spinner> }
              {{ assignee() ? 'Re-suggest' : 'Suggest assignee' }}
            </button>
          </div>

          @if (assigneeError()) {
            <p class="text-xs text-red-600 mb-2">{{ assigneeError() }}</p>
          }

          @if (assignee(); as a) {
            <div class="border border-gray-200 rounded-lg p-3 flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-gradient-to-br from-forge-400 to-forge-600 flex items-center justify-center text-zinc-900 font-bold text-xs">
                {{ initials(a.fullName) }}
              </div>
              <div class="flex-1 min-w-0">
                <div class="text-sm font-medium text-gray-900">{{ a.fullName }}</div>
                <div class="text-xs text-gray-500">{{ a.reasoning }}</div>
              </div>
              <button mat-stroked-button class="!text-xs" [disabled]="assigning()" (click)="applyAssignee()">
                Assign
              </button>
            </div>
            @if (assigned()) {
              <p class="text-xs text-green-700 mt-1">Assigned to {{ a.fullName }}.</p>
            }
          }
        </div>

      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close(appliedCount() !== null || assigned())">Close</button>
    </mat-dialog-actions>
  `
})
export class TaskAiDialogComponent {
  private ai = inject(AiService);
  private tasksService = inject(TasksService);
  ref = inject(MatDialogRef<TaskAiDialogComponent>);
  data = inject<TaskAiDialogData>(MAT_DIALOG_DATA);

  subtasks = signal<string[]>([]);
  selected = signal<Set<string>>(new Set());
  breakdownLoading = signal(false);
  breakdownError = signal<string | null>(null);
  applyingBreakdown = signal(false);
  appliedCount = signal<number | null>(null);

  assignee = signal<{ userId: string; fullName: string; reasoning: string } | null>(null);
  assigneeLoading = signal(false);
  assigneeError = signal<string | null>(null);
  assigning = signal(false);
  assigned = signal(false);

  loadBreakdown() {
    this.breakdownLoading.set(true);
    this.breakdownError.set(null);
    this.appliedCount.set(null);
    this.ai.suggestBreakdown(this.data.taskId).subscribe({
      next: res => {
        this.subtasks.set(res.subtasks);
        this.selected.set(new Set(res.subtasks));
        this.breakdownLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.breakdownError.set(aiErrorMessage(err));
        this.breakdownLoading.set(false);
      }
    });
  }

  toggleSelected(title: string) {
    const next = new Set(this.selected());
    next.has(title) ? next.delete(title) : next.add(title);
    this.selected.set(next);
  }

  applySelected() {
    this.applyingBreakdown.set(true);
    this.ai.applyBreakdown(this.data.taskId, { subtaskTitles: Array.from(this.selected()) }).subscribe({
      next: count => {
        this.appliedCount.set(count);
        this.applyingBreakdown.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.breakdownError.set(aiErrorMessage(err));
        this.applyingBreakdown.set(false);
      }
    });
  }

  loadAssignee() {
    this.assigneeLoading.set(true);
    this.assigneeError.set(null);
    this.assigned.set(false);
    this.ai.suggestAssignee(this.data.taskId).subscribe({
      next: res => {
        this.assignee.set(res);
        this.assigneeLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.assigneeError.set(aiErrorMessage(err));
        this.assigneeLoading.set(false);
      }
    });
  }

  applyAssignee() {
    const a = this.assignee();
    if (!a) return;
    this.assigning.set(true);
    this.tasksService.assignTask(this.data.taskId, a.userId, this.data.boardId).subscribe({
      next: () => {
        this.assigned.set(true);
        this.assigning.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.assigneeError.set(aiErrorMessage(err));
        this.assigning.set(false);
      }
    });
  }

  initials(name: string): string {
    return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
  }
}
