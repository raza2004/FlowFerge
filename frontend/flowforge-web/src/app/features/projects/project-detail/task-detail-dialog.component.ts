import { Component, ElementRef, ViewChild, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TasksService } from '../../../shared/services/tasks.service';
import { AuthService } from '../../../core/services/auth.service';
import { TenantMemberDto } from '../../../shared/models/auth.models';
import {
  TaskDetailDto, TaskCommentDto, LabelDto, TASK_TYPES, TASK_PRIORITIES
} from '../../../shared/models/project.models';

export interface TaskDetailDialogData {
  taskId: string;
  boardId: string;
  projectId: string;
  members: TenantMemberDto[];
  /** Called after every successful change so the board behind the dialog can refresh. */
  onChanged: () => void;
}

interface CommentSegment {
  text: string;
  mention: boolean;
}

const LABEL_COLORS = ['#6449E0', '#0EA97C', '#E5484D', '#F59E0B', '#0EA5E9', '#EC4899', '#71717A'];

@Component({
  selector: 'app-task-detail-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule
  ],
  templateUrl: './task-detail-dialog.component.html'
})
export class TaskDetailDialogComponent implements OnInit {
  private tasks = inject(TasksService);
  private auth = inject(AuthService);
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<TaskDetailDialogComponent>);
  data = inject<TaskDetailDialogData>(MAT_DIALOG_DATA);

  @ViewChild('commentBox') commentBox?: ElementRef<HTMLTextAreaElement>;

  readonly types = TASK_TYPES;
  readonly priorities = TASK_PRIORITIES;
  readonly labelColors = LABEL_COLORS;
  readonly currentUserId = this.auth.user()?.id ?? '';

  task = signal<TaskDetailDto | null>(null);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  confirmingDelete = signal(false);

  projectLabels = signal<LabelDto[]>([]);
  labelPickerOpen = signal(false);
  newLabelName = '';
  newLabelColor = LABEL_COLORS[0];

  newSubtaskTitle = '';

  logHours: number | null = null;
  logNote = '';
  showAllTime = signal(false);

  newComment = '';
  mentionQuery = signal<string | null>(null);
  editingCommentId = signal<string | null>(null);
  editingText = '';

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(500)]],
    description: [''],
    type: [0, Validators.required],
    priority: [2, Validators.required],
    dueDate: [''],
    estimatedHours: [null as number | null, Validators.min(0)],
    storyPoints: [null as number | null, [Validators.min(0), Validators.max(100)]]
  });

  availableLabels = computed(() => {
    const attached = new Set(this.task()?.labels.map(l => l.id) ?? []);
    return this.projectLabels().filter(l => !attached.has(l.id));
  });

  mentionSuggestions = computed(() => {
    const query = this.mentionQuery();
    if (query === null) return [];
    const q = query.toLowerCase();
    return this.data.members
      .filter(m => m.userId !== this.currentUserId && m.fullName.toLowerCase().includes(q))
      .slice(0, 6);
  });

  ngOnInit() {
    this.loadTask(this.data.taskId);
    this.tasks.getProjectLabels(this.data.projectId).subscribe(labels => this.projectLabels.set(labels));
  }

  loadTask(taskId: string) {
    this.loading.set(true);
    this.confirmingDelete.set(false);
    this.tasks.getDetail(taskId).subscribe({
      next: task => {
        this.task.set(task);
        this.resetForm(task);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load this task. It may have been deleted.');
        this.loading.set(false);
      }
    });
  }

  private resetForm(task: TaskDetailDto) {
    this.form.reset({
      title: task.title,
      description: task.description ?? '',
      type: this.types.find(t => t.label === task.type)?.value ?? 0,
      priority: this.priorities.find(p => p.label === task.priority)?.value ?? 2,
      dueDate: task.dueDate ? task.dueDate.substring(0, 10) : '',
      estimatedHours: task.estimatedHours,
      storyPoints: task.storyPoints
    });
  }

  resetFormFromTask() {
    const task = this.task();
    if (task) this.resetForm(task);
  }

  completedSubtasks(task: TaskDetailDto): number {
    return task.subtasks.filter(s => s.isCompleted).length;
  }

  private changed() {
    this.data.onChanged();
  }

  private showError(err: HttpErrorResponse, fallback: string) {
    this.error.set(err.error?.detail ?? fallback);
  }

  // ── Details ─────────────────────────────────────────────────────────────

  save() {
    const task = this.task();
    if (!task || this.form.invalid) return;
    const v = this.form.getRawValue();

    this.saving.set(true);
    this.error.set(null);
    this.tasks.update(task.id, {
      title: v.title!,
      description: v.description || null,
      type: v.type!,
      priority: v.priority!,
      dueDate: v.dueDate ? `${v.dueDate}T00:00:00Z` : null,
      estimatedHours: v.estimatedHours ?? null,
      storyPoints: v.storyPoints ?? null,
      boardId: this.data.boardId
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.changed();
        this.loadTask(task.id);
      },
      error: err => {
        this.saving.set(false);
        this.showError(err, 'Could not save changes.');
      }
    });
  }

  assign(userId: string) {
    const task = this.task();
    if (!task || !userId || userId === task.assigneeId) return;
    this.tasks.assignTask(task.id, userId, this.data.boardId).subscribe({
      next: () => {
        this.changed();
        this.loadTask(task.id);
      },
      error: err => this.showError(err, 'Could not change the assignee.')
    });
  }

  toggleWatch() {
    const task = this.task();
    if (!task) return;
    const watch = !task.isWatching;
    this.tasks.setWatching(task.id, watch).subscribe({
      next: () => this.loadTask(task.id),
      error: err => this.showError(err, 'Could not update watching.')
    });
  }

  deleteTask() {
    const task = this.task();
    if (!task) return;
    this.tasks.delete(task.id, this.data.boardId).subscribe({
      next: () => {
        this.changed();
        this.ref.close('deleted');
      },
      error: err => this.showError(err, 'Could not delete this task.')
    });
  }

  // ── Labels ──────────────────────────────────────────────────────────────

  toggleLabel(label: LabelDto, attach: boolean) {
    const task = this.task();
    if (!task) return;
    this.tasks.setLabel(task.id, label.id, attach, this.data.boardId).subscribe({
      next: () => {
        this.task.update(t => t && ({
          ...t,
          labels: attach
            ? [...t.labels, label].sort((a, b) => a.name.localeCompare(b.name))
            : t.labels.filter(l => l.id !== label.id)
        }));
        this.changed();
      },
      error: err => this.showError(err, 'Could not update labels.')
    });
  }

  createLabel() {
    const name = this.newLabelName.trim();
    if (!name) return;
    this.tasks.createLabel(this.data.projectId, name, this.newLabelColor).subscribe({
      next: label => {
        this.projectLabels.update(list => [...list, label].sort((a, b) => a.name.localeCompare(b.name)));
        this.newLabelName = '';
        this.toggleLabel(label, true);
      },
      error: err => this.showError(err, 'Could not create the label.')
    });
  }

  // ── Subtasks ────────────────────────────────────────────────────────────

  addSubtask() {
    const task = this.task();
    const title = this.newSubtaskTitle.trim();
    if (!task || !title) return;
    this.tasks.createSubtask(task.id, title, this.data.boardId).subscribe({
      next: subtask => {
        this.newSubtaskTitle = '';
        this.task.update(t => t && ({ ...t, subtasks: [...t.subtasks, subtask] }));
        this.changed();
      },
      error: err => this.showError(err, 'Could not add the subtask.')
    });
  }

  // ── Time tracking ───────────────────────────────────────────────────────

  logTime() {
    const task = this.task();
    const hours = this.logHours;
    if (!task || !hours || hours <= 0 || hours > 24) return;
    this.tasks.logTime(task.id, hours, null, this.logNote.trim() || null).subscribe({
      next: entry => {
        this.logHours = null;
        this.logNote = '';
        this.task.update(t => t && ({
          ...t,
          actualHours: Math.round(((t.actualHours ?? 0) + entry.hours) * 100) / 100,
          timeEntries: [entry, ...t.timeEntries]
        }));
      },
      error: err => this.showError(err, 'Could not log time.')
    });
  }

  deleteTimeEntry(entryId: string, hours: number) {
    this.tasks.deleteTimeEntry(entryId).subscribe({
      next: () => this.task.update(t => t && ({
        ...t,
        actualHours: Math.max(0, Math.round(((t.actualHours ?? 0) - hours) * 100) / 100) || null,
        timeEntries: t.timeEntries.filter(e => e.id !== entryId)
      })),
      error: err => this.showError(err, 'Could not delete that time entry.')
    });
  }

  /** Percentage of the estimate used so far, capped at 100 for the bar width. */
  timeProgress(task: TaskDetailDto): number {
    if (!task.estimatedHours) return 0;
    return Math.min(100, ((task.actualHours ?? 0) / task.estimatedHours) * 100);
  }

  // ── Comments + @mentions ────────────────────────────────────────────────

  onCommentInput() {
    const box = this.commentBox?.nativeElement;
    if (!box) return;
    const beforeCaret = box.value.substring(0, box.selectionStart ?? box.value.length);
    const match = /(?:^|\s)@([\p{L}\p{N}]*(?: [\p{L}\p{N}]*)?)$/u.exec(beforeCaret);
    this.mentionQuery.set(match ? match[1] : null);
  }

  onCommentKeydown(event: KeyboardEvent) {
    const suggestions = this.mentionSuggestions();
    if (event.key === 'Enter' && suggestions.length > 0) {
      event.preventDefault();
      this.insertMention(suggestions[0]);
    } else if (event.key === 'Escape' && this.mentionQuery() !== null) {
      event.stopPropagation();
      this.mentionQuery.set(null);
    } else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      this.postComment();
    }
  }

  insertMention(member: TenantMemberDto) {
    const box = this.commentBox?.nativeElement;
    if (!box) return;
    const caret = box.selectionStart ?? box.value.length;
    const before = box.value.substring(0, caret).replace(/@[^@]*$/, `@${member.fullName} `);
    const after = box.value.substring(caret);
    this.newComment = before + after;
    this.mentionQuery.set(null);
    queueMicrotask(() => {
      box.focus();
      box.setSelectionRange(before.length, before.length);
    });
  }

  postComment() {
    const task = this.task();
    const content = this.newComment.trim();
    if (!task || !content) return;

    const mentioned = this.data.members
      .filter(m => content.includes(`@${m.fullName}`))
      .map(m => m.userId);

    this.tasks.addComment(task.id, content, mentioned, this.data.boardId).subscribe({
      next: comment => {
        this.newComment = '';
        this.mentionQuery.set(null);
        this.task.update(t => t && ({ ...t, comments: [...t.comments, comment], isWatching: true }));
        this.changed();
      },
      error: err => this.showError(err, 'Could not post the comment.')
    });
  }

  startEdit(comment: TaskCommentDto) {
    this.editingCommentId.set(comment.id);
    this.editingText = comment.content;
  }

  saveEdit(comment: TaskCommentDto) {
    const content = this.editingText.trim();
    if (!content) return;
    this.tasks.editComment(comment.id, content).subscribe({
      next: () => {
        this.task.update(t => t && ({
          ...t,
          comments: t.comments.map(c => c.id === comment.id ? { ...c, content, isEdited: true } : c)
        }));
        this.editingCommentId.set(null);
      },
      error: err => this.showError(err, 'Could not edit the comment.')
    });
  }

  deleteComment(comment: TaskCommentDto) {
    this.tasks.deleteComment(comment.id).subscribe({
      next: () => {
        this.task.update(t => t && ({ ...t, comments: t.comments.filter(c => c.id !== comment.id) }));
        this.changed();
      },
      error: err => this.showError(err, 'Could not delete the comment.')
    });
  }

  /** Splits a comment into plain/mention runs so mentions can be highlighted without innerHTML. */
  segments(comment: TaskCommentDto): CommentSegment[] {
    const names = this.data.members
      .filter(m => comment.mentionedUserIds.includes(m.userId))
      .map(m => `@${m.fullName}`);
    if (names.length === 0) return [{ text: comment.content, mention: false }];

    const escaped = names.map(n => n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
    return comment.content
      .split(new RegExp(`(${escaped.join('|')})`, 'g'))
      .filter(part => part.length > 0)
      .map(part => ({ text: part, mention: names.includes(part) }));
  }

  initials(name: string | null | undefined): string {
    if (!name) return '?';
    return name.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}
