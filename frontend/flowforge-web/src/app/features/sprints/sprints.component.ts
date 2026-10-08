import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SprintsService } from '../../shared/services/sprints.service';
import { ProjectsService } from '../../shared/services/projects.service';
import { BurndownChartComponent } from '../../shared/components/burndown-chart.component';
import {
  ProjectDto, SprintDetailDto, SprintDto, SprintForm, SprintTaskDto
} from '../../shared/models/project.models';

const isoDate = (d: Date) => d.toISOString().substring(0, 10);

// ── New / edit sprint ─────────────────────────────────────────────────────

export interface SprintFormDialogData {
  sprint?: SprintDto;
}

@Component({
  selector: 'app-sprint-form-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.sprint ? 'Edit sprint' : 'New sprint' }}</h2>
    <mat-dialog-content>
      <div class="space-y-4 pt-1 w-[400px] max-w-full">
        <label class="block">
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Name</span>
          <input [(ngModel)]="form.name" maxlength="200" placeholder="Sprint 1"
                 class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none">
        </label>
        <label class="block">
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Goal</span>
          <textarea [(ngModel)]="form.goal" rows="2" maxlength="2000" placeholder="What should this sprint achieve?"
                    class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none resize-none"></textarea>
        </label>
        <div class="grid grid-cols-2 gap-3">
          <label class="block">
            <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Starts</span>
            <input type="date" [(ngModel)]="form.startDate"
                   class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none">
          </label>
          <label class="block">
            <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Ends</span>
            <input type="date" [(ngModel)]="form.endDate" [min]="form.startDate"
                   class="mt-1 w-full text-sm border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none">
          </label>
        </div>
        @if (problem()) { <p class="text-xs text-red-600">{{ problem() }}</p> }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="!!problem()" (click)="ref.close(form)">
        {{ data.sprint ? 'Save' : 'Create sprint' }}
      </button>
    </mat-dialog-actions>
  `
})
export class SprintFormDialogComponent {
  ref = inject(MatDialogRef<SprintFormDialogComponent>);
  data = inject<SprintFormDialogData>(MAT_DIALOG_DATA);

  form: SprintForm = {
    name: this.data.sprint?.name ?? '',
    goal: this.data.sprint?.goal ?? '',
    startDate: this.data.sprint ? this.data.sprint.startDate.substring(0, 10) : isoDate(new Date()),
    endDate: this.data.sprint ? this.data.sprint.endDate.substring(0, 10) : isoDate(new Date(Date.now() + 13 * 86_400_000))
  };

  /** Mirrors the server rules so the button explains itself instead of failing after the click. */
  problem = () => {
    if (!this.form.name.trim()) return 'Give the sprint a name.';
    if (!this.form.startDate || !this.form.endDate) return 'Choose a start and end date.';
    const days = (Date.parse(this.form.endDate) - Date.parse(this.form.startDate)) / 86_400_000;
    if (days <= 0) return 'The end date must be after the start date.';
    if (days > 60) return 'A sprint can be at most 60 days long.';
    return null;
  };
}

// ── Complete sprint ───────────────────────────────────────────────────────

export interface CompleteSprintDialogData {
  sprint: SprintDto;
  unfinished: number;
  plannedSprints: SprintDto[];
}

@Component({
  selector: 'app-complete-sprint-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Complete "{{ data.sprint.name }}"</h2>
    <mat-dialog-content>
      <div class="space-y-4 pt-1 w-[420px] max-w-full text-sm text-ink-soft">
        <p>{{ data.sprint.doneCount }} of {{ data.sprint.taskCount }} tasks are done.</p>

        @if (data.unfinished > 0) {
          <label class="block">
            <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">
              Move the {{ data.unfinished }} unfinished task(s) to
            </span>
            <select [(ngModel)]="destination"
                    class="mt-1 w-full border border-line rounded-md px-3 py-2 bg-white focus:border-forge-400 focus:outline-none">
              <option [ngValue]="null">Backlog</option>
              @for (s of data.plannedSprints; track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select>
          </label>
        }

        <label class="block">
          <span class="text-xs font-medium uppercase tracking-wide text-ink-muted">Retrospective notes</span>
          <textarea [(ngModel)]="notes" rows="4" maxlength="10000"
                    placeholder="What went well? What should change next sprint?"
                    class="mt-1 w-full border border-line rounded-md px-3 py-2 focus:border-forge-400 focus:outline-none resize-y"></textarea>
        </label>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" (click)="ref.close({ notes: notes.trim() || null, destination })">Complete sprint</button>
    </mat-dialog-actions>
  `
})
export class CompleteSprintDialogComponent {
  ref = inject(MatDialogRef<CompleteSprintDialogComponent>);
  data = inject<CompleteSprintDialogData>(MAT_DIALOG_DATA);
  notes = '';
  destination: string | null = null;
}

// ── Page ──────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-sprints',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatDialogModule, MatButtonModule, MatIconModule, MatTooltipModule, BurndownChartComponent],
  templateUrl: './sprints.component.html'
})
export class SprintsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private sprintsService = inject(SprintsService);
  private projectsService = inject(ProjectsService);
  private dialog = inject(MatDialog);

  projectId = '';
  project = signal<ProjectDto | null>(null);
  sprints = signal<SprintDto[]>([]);
  backlog = signal<SprintTaskDto[]>([]);
  details = signal<Record<string, SprintDetailDto>>({});
  loading = signal(true);
  error = signal<string | null>(null);
  openHistoryId = signal<string | null>(null);
  retroDrafts: Record<string, string> = {};
  savedRetroId = signal<string | null>(null);

  active = computed(() => this.sprints().find(s => s.status === 'Active') ?? null);
  planned = computed(() => this.sprints().filter(s => s.status === 'Planning'));
  history = computed(() => this.sprints().filter(s => s.status === 'Completed' || s.status === 'Cancelled'));
  openSprints = computed(() => this.sprints().filter(s => s.status === 'Active' || s.status === 'Planning'));

  ngOnInit() {
    this.projectId = this.route.snapshot.paramMap.get('id') ?? '';
    this.projectsService.getById(this.projectId).subscribe(p => this.project.set(p));
    this.reload();
  }

  reload() {
    forkJoin({
      sprints: this.sprintsService.getSprints(this.projectId),
      backlog: this.sprintsService.getBacklog(this.projectId)
    }).subscribe({
      next: ({ sprints, backlog }) => {
        this.sprints.set(sprints);
        this.backlog.set(backlog);
        this.loading.set(false);
        sprints.filter(s => s.status === 'Active' || s.status === 'Planning').forEach(s => this.loadDetail(s.id));
        const openHistory = this.openHistoryId();
        if (openHistory) this.loadDetail(openHistory);
      },
      error: () => {
        this.error.set('Could not load sprints.');
        this.loading.set(false);
      }
    });
  }

  private loadDetail(sprintId: string) {
    this.sprintsService.getDetail(sprintId).subscribe(detail => {
      this.details.update(d => ({ ...d, [sprintId]: detail }));
      this.retroDrafts[sprintId] ??= detail.sprint.retrospectiveNotes ?? '';
    });
  }

  private fail(err: HttpErrorResponse, fallback: string) {
    this.error.set(err.error?.detail ?? fallback);
  }

  tasksOf(sprintId: string): SprintTaskDto[] {
    return this.details()[sprintId]?.tasks ?? [];
  }

  progress(s: SprintDto): number {
    if (s.totalPoints > 0) return (s.donePoints / s.totalPoints) * 100;
    return s.taskCount > 0 ? (s.doneCount / s.taskCount) * 100 : 0;
  }

  daysLeft(s: SprintDto): number {
    return Math.ceil((Date.parse(s.endDate) - Date.now()) / 86_400_000);
  }

  // ── Sprint actions ────────────────────────────────────────────────────

  createSprint() {
    this.dialog.open(SprintFormDialogComponent, { data: {} as SprintFormDialogData })
      .afterClosed().subscribe((form?: SprintForm) => {
        if (!form) return;
        this.sprintsService.create(this.projectId, form).subscribe({
          next: () => this.reload(),
          error: err => this.fail(err, 'Could not create the sprint.')
        });
      });
  }

  editSprint(sprint: SprintDto) {
    this.dialog.open(SprintFormDialogComponent, { data: { sprint } as SprintFormDialogData })
      .afterClosed().subscribe((form?: SprintForm) => {
        if (!form) return;
        this.sprintsService.update(sprint.id, form).subscribe({
          next: () => this.reload(),
          error: err => this.fail(err, 'Could not update the sprint.')
        });
      });
  }

  startSprint(sprint: SprintDto) {
    this.sprintsService.start(sprint.id).subscribe({
      next: () => this.reload(),
      error: err => this.fail(err, 'Could not start the sprint.')
    });
  }

  completeSprint(sprint: SprintDto) {
    const data: CompleteSprintDialogData = {
      sprint,
      unfinished: sprint.taskCount - sprint.doneCount,
      plannedSprints: this.planned()
    };
    this.dialog.open(CompleteSprintDialogComponent, { data })
      .afterClosed().subscribe((result?: { notes: string | null; destination: string | null }) => {
        if (!result) return;
        this.sprintsService.complete(sprint.id, result.notes, result.destination).subscribe({
          next: () => {
            this.retroDrafts[sprint.id] = result.notes ?? '';
            this.reload();
          },
          error: err => this.fail(err, 'Could not complete the sprint.')
        });
      });
  }

  cancelSprint(sprint: SprintDto) {
    if (!confirm(`Cancel "${sprint.name}"? Its tasks go back to the backlog.`)) return;
    this.sprintsService.cancel(sprint.id).subscribe({
      next: () => this.reload(),
      error: err => this.fail(err, 'Could not cancel the sprint.')
    });
  }

  toggleHistory(sprint: SprintDto) {
    const opening = this.openHistoryId() !== sprint.id;
    this.openHistoryId.set(opening ? sprint.id : null);
    if (opening && !this.details()[sprint.id]) this.loadDetail(sprint.id);
  }

  saveRetro(sprint: SprintDto) {
    this.sprintsService.saveRetrospective(sprint.id, this.retroDrafts[sprint.id]?.trim() || null).subscribe({
      next: () => {
        this.savedRetroId.set(sprint.id);
        setTimeout(() => this.savedRetroId.set(null), 2000);
        this.sprints.update(list => list.map(s =>
          s.id === sprint.id ? { ...s, retrospectiveNotes: this.retroDrafts[sprint.id]?.trim() || null } : s));
      },
      error: err => this.fail(err, 'Could not save the retrospective.')
    });
  }

  // ── Planning ──────────────────────────────────────────────────────────

  moveTask(task: SprintTaskDto, sprintId: string | null) {
    this.sprintsService.assignTask(task.id, sprintId).subscribe({
      next: () => this.reload(),
      error: err => this.fail(err, 'Could not move that task.')
    });
  }

  onPickSprint(task: SprintTaskDto, select: HTMLSelectElement) {
    const sprintId = select.value;
    select.value = '';
    if (sprintId) this.moveTask(task, sprintId);
  }

  initials(name: string | null): string {
    return (name ?? '').split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}
