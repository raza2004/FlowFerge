import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { BoardListDto, ListSettings } from '../../../shared/models/project.models';

const LIST_COLORS = ['#94a3b8', '#3b82f6', '#6449E0', '#F59E0B', '#EC4899', '#0EA5E9', '#10b981', '#E5484D'];

export interface ListSettingsDialogData {
  /** Absent when creating a new list. */
  list?: BoardListDto;
}

@Component({
  selector: 'app-list-settings-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatSlideToggleModule],
  template: `
    <h2 mat-dialog-title>{{ data.list ? 'Edit list' : 'New list' }}</h2>
    <mat-dialog-content>
      <div class="space-y-4 pt-1 w-[360px] max-w-full">
        <label class="block">
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Name</span>
          <input [(ngModel)]="settings.name" maxlength="100" autofocus
                 class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none">
        </label>

        <div>
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Color</span>
          <div class="flex gap-2 mt-1.5">
            @for (c of colors; track c) {
              <button type="button" class="w-6 h-6 rounded-full ring-offset-2 transition"
                      [style.background]="c" [class.ring-2]="settings.color === c" [style.--tw-ring-color]="c"
                      (click)="settings.color = c"></button>
            }
          </div>
        </div>

        <label class="block">
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">WIP limit</span>
          <input type="number" min="1" max="999" [(ngModel)]="settings.wipLimit" placeholder="No limit"
                 class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none">
          <span class="text-xs text-ink-faint">The card count turns red when a list goes over this.</span>
        </label>

        @if (data.list) {
          <mat-slide-toggle [(ngModel)]="settings.isDoneColumn">
            <span class="text-sm">Tasks in this list count as done</span>
          </mat-slide-toggle>
        }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="!valid()" (click)="save()">
        {{ data.list ? 'Save' : 'Add list' }}
      </button>
    </mat-dialog-actions>
  `
})
export class ListSettingsDialogComponent {
  ref = inject(MatDialogRef<ListSettingsDialogComponent>);
  data = inject<ListSettingsDialogData>(MAT_DIALOG_DATA);
  readonly colors = LIST_COLORS;

  settings: ListSettings = {
    name: this.data.list?.name ?? '',
    color: this.data.list?.color ?? LIST_COLORS[0],
    wipLimit: this.data.list?.wipLimit ?? null,
    isDoneColumn: this.data.list?.isDoneColumn ?? false
  };

  valid(): boolean {
    const wip = this.settings.wipLimit;
    return this.settings.name.trim().length > 0 && (wip === null || (wip >= 1 && wip <= 999));
  }

  save() {
    if (!this.valid()) return;
    // An emptied number input binds as null; normalize so "no limit" is always null.
    const wip = this.settings.wipLimit;
    this.ref.close({ ...this.settings, name: this.settings.name.trim(), wipLimit: wip ? Math.floor(wip) : null } as ListSettings);
  }
}

export interface DeleteListDialogData {
  list: BoardListDto;
  otherLists: BoardListDto[];
}

@Component({
  selector: 'app-delete-list-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Delete "{{ data.list.name }}"?</h2>
    <mat-dialog-content>
      <div class="w-[360px] max-w-full text-sm text-ink-soft space-y-3">
        @if (data.list.tasks.length > 0) {
          <p>This list has {{ data.list.tasks.length }} task(s). Move them to:</p>
          <select [(ngModel)]="targetId"
                  class="w-full border border-line rounded-md px-3 py-2 bg-white focus:border-forge-400 focus:outline-none">
            @for (l of data.otherLists; track l.id) { <option [value]="l.id">{{ l.name }}</option> }
          </select>
        } @else {
          <p>The list is empty, so nothing else is affected.</p>
        }
        <p class="text-xs text-ink-faint">Automation rules that trigger on this list are removed too.</p>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-flat-button class="!bg-red-600 !text-white" (click)="confirm()">Delete list</button>
    </mat-dialog-actions>
  `
})
export class DeleteListDialogComponent {
  ref = inject(MatDialogRef<DeleteListDialogComponent>);
  data = inject<DeleteListDialogData>(MAT_DIALOG_DATA);
  targetId = this.data.otherLists[0]?.id ?? '';

  confirm() {
    this.ref.close({ moveTasksTo: this.data.list.tasks.length > 0 ? this.targetId : null });
  }
}
