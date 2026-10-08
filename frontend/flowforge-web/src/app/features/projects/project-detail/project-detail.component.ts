import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogRef } from '@angular/material/dialog';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { HttpErrorResponse } from '@angular/common/http';
import { ProjectsService } from '../../../shared/services/projects.service';
import { BoardsService } from '../../../shared/services/boards.service';
import { TasksService } from '../../../shared/services/tasks.service';
import { AutomationsService } from '../../../shared/services/automations.service';
import { DashboardService } from '../../../shared/services/dashboard.service';
import { AiService } from '../../../shared/services/ai.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { ProjectDto, BoardDto, BoardListDto, TaskCardDto } from '../../../shared/models/project.models';
import { AutomationRuleDto, AutomationActionType, AutomationTriggerType } from '../../../shared/models/automation.models';
import { TenantMemberDto } from '../../../shared/models/auth.models';
import { TaskAiDialogComponent, TaskAiDialogData } from './task-ai-dialog.component';
import { TaskDetailDialogComponent, TaskDetailDialogData } from './task-detail-dialog.component';
import {
  ListSettingsDialogComponent, ListSettingsDialogData, DeleteListDialogComponent, DeleteListDialogData
} from './list-dialogs.component';
import { ListSettings, SprintDto } from '../../../shared/models/project.models';
import { ProjectBlockersDto } from '../../../shared/models/ai.models';
import { SprintsService } from '../../../shared/services/sprints.service';
import { FeaturesService, FEATURE_AI, FEATURE_SPRINTS } from '../../../shared/services/features.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-create-task-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>New task</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="space-y-3 pt-2">
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title">
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>
        <div class="grid grid-cols-2 gap-3">
          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select formControlName="type">
              <mat-option [value]="0">Task</mat-option>
              <mat-option [value]="1">Bug</mat-option>
              <mat-option [value]="2">Feature</mat-option>
              <mat-option [value]="3">Story</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              <mat-option [value]="1">Low</mat-option>
              <mat-option [value]="2">Medium</mat-option>
              <mat-option [value]="3">High</mat-option>
              <mat-option [value]="5">Critical</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="create()">Create</button>
    </mat-dialog-actions>
  `
})
export class CreateTaskDialogComponent {
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<CreateTaskDialogComponent>);

  form = this.fb.group({
    title: ['', Validators.required],
    description: [''],
    type: [0, Validators.required],
    priority: [2, Validators.required]
  });

  create() { this.ref.close(this.form.value); }
}

export interface AutomationDialogData {
  lists: BoardListDto[];
  members: TenantMemberDto[];
}

@Component({
  selector: 'app-create-automation-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>New automation</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="space-y-3 pt-2">
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Rule name</mat-label>
          <input matInput formControlName="name" placeholder="e.g. Notify on Done">
        </mat-form-field>

        <div class="flex items-center gap-2 text-sm text-ink-muted">
          <span>When a task is moved to</span>
        </div>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>List</mat-label>
          <mat-select formControlName="triggerListId">
            @for (list of data.lists; track list.id) {
              <mat-option [value]="list.id">{{ list.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <div class="flex items-center gap-2 text-sm text-ink-muted">
          <span>then</span>
        </div>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Action</mat-label>
          <mat-select formControlName="actionType">
            <mat-option [value]="0">Notify</mat-option>
            <mat-option [value]="1">Assign to</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Member</mat-label>
          <mat-select formControlName="actionUserId">
            @for (m of data.members; track m.userId) {
              <mat-option [value]="m.userId">{{ m.fullName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="create()">Create</button>
    </mat-dialog-actions>
  `
})
export class CreateAutomationDialogComponent {
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<CreateAutomationDialogComponent>);
  data = inject<AutomationDialogData>(MAT_DIALOG_DATA);

  form = this.fb.group({
    name: ['', Validators.required],
    triggerListId: ['', Validators.required],
    actionType: [0, Validators.required],
    actionUserId: ['', Validators.required]
  });

  create() { this.ref.close(this.form.value); }
}

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink, DragDropModule,
    MatCardModule, MatIconModule, MatButtonModule, MatChipsModule,
    MatMenuModule, MatDialogModule, MatSlideToggleModule, MatTooltipModule
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss'
})
export class ProjectDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private projectsService = inject(ProjectsService);
  private boardsService = inject(BoardsService);
  private tasksService = inject(TasksService);
  private automationsService = inject(AutomationsService);
  private dashboardService = inject(DashboardService);
  private aiService = inject(AiService);
  private signalr = inject(SignalrService);
  private sprintsService = inject(SprintsService);
  features = inject(FeaturesService);
  readonly FEATURE_AI = FEATURE_AI;
  readonly FEATURE_SPRINTS = FEATURE_SPRINTS;
  private dialog = inject(MatDialog);

  project = signal<ProjectDto | null>(null);
  board = signal<BoardDto | null>(null);
  isLoading = signal(true);

  automations = signal<AutomationRuleDto[]>([]);
  members = signal<TenantMemberDto[]>([]);

  aiSummary = signal<string | null>(null);
  aiSummaryLoading = signal(false);
  aiSummaryError = signal<string | null>(null);

  blockers = signal<ProjectBlockersDto | null>(null);
  blockersLoading = signal(false);
  blockersError = signal<string | null>(null);

  scanForBlockers() {
    const project = this.project();
    if (!project) return;

    this.blockersLoading.set(true);
    this.blockersError.set(null);
    this.aiService.getProjectBlockers(project.id).subscribe({
      next: result => {
        this.blockers.set(result);
        this.blockersLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.blockersError.set(err.error?.detail ?? 'Could not scan for blockers right now.');
        this.blockersLoading.set(false);
      }
    });
  }

  severityClass(severity: string): string {
    return severity === 'High' ? 'bg-red-100 text-red-700'
      : severity === 'Medium' ? 'bg-amber-100 text-amber-700' : 'bg-sunken text-ink-soft';
  }

  private realtimeSubs = new Subscription();
  private reloadTimer?: ReturnType<typeof setTimeout>;
  private justDragged = false;

  get listConnectedTo(): string[] {
    return this.board()?.lists.map(l => `list-${l.id}`) ?? [];
  }

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.projectsService.getById(id).subscribe(p => this.project.set(p));
    this.automationsService.getForProject(id).subscribe(rules => this.automations.set(rules));
    this.dashboardService.getTeamMembers().subscribe(members => this.members.set(members));
    this.loadActiveSprint(id);

    this.boardsService.getProjectBoards(id).subscribe(async boards => {
      if (boards.length > 0) {
        this.board.set(boards[0]);
        // Show the board right away; live updates are a bonus, so a failing realtime
        // connection must never leave the page stuck on its loading skeleton.
        this.isLoading.set(false);
        try {
          await this.signalr.startConnection();
          await this.signalr.joinBoard(boards[0].id);
        } catch {
          return;
        }

        this.realtimeSubs.add(this.signalr.taskMoved$.subscribe(event => this.applyRemoteMove(event)));
        this.realtimeSubs.add(this.signalr.taskCreated$.subscribe(task => this.applyRemoteCreate(task)));
        this.realtimeSubs.add(this.signalr.taskUpdated$.subscribe(() => this.scheduleReload()));
        this.realtimeSubs.add(this.signalr.taskDeleted$.subscribe(e => this.removeTaskLocally(e.taskId)));
        this.realtimeSubs.add(this.signalr.boardChanged$.subscribe(() => this.scheduleReload()));
        return;
      }
      this.isLoading.set(false);
    });
  }

  async ngOnDestroy() {
    this.realtimeSubs.unsubscribe();
    clearTimeout(this.reloadTimer);
    const b = this.board();
    if (b) await this.signalr.leaveBoard(b.id);
  }

  private loadActiveSprint(projectId: string) {
    this.sprintsService.getSprints(projectId).subscribe(sprints => {
      const active = sprints.find(s => s.status === 'Active') ?? null;
      this.activeSprint.set(active);
      if (!active) this.sprintFilter.set('all');
    });
  }

  onDragStarted() {
    this.justDragged = true;
  }

  onDragEnded() {
    // The browser fires a click right after the drop; ignore it so dragging never opens the task.
    setTimeout(() => this.justDragged = false, 100);
  }

  openTaskDetail(task: TaskCardDto) {
    if (this.justDragged) return;
    this.openTaskById(task.id);
  }

  openTaskById(taskId: string) {
    const board = this.board();
    const project = this.project();
    if (!board || !project) return;

    this.dialog.open(TaskDetailDialogComponent, {
      width: '880px',
      maxWidth: '95vw',
      autoFocus: false,
      data: {
        taskId,
        boardId: board.id,
        projectId: project.id,
        members: this.members(),
        onChanged: () => this.scheduleReload()
      } as TaskDetailDialogData
    });
  }

  boardError = signal<string | null>(null);

  openAddList() {
    const board = this.board();
    if (!board) return;
    this.dialog.open(ListSettingsDialogComponent, { data: {} as ListSettingsDialogData })
      .afterClosed().subscribe((settings?: ListSettings) => {
        if (!settings) return;
        this.boardsService.createList(board.id, settings).subscribe({
          next: () => this.reloadBoard(),
          error: err => this.boardError.set(err.error?.detail ?? 'Could not add the list.')
        });
      });
  }

  editList(list: BoardListDto) {
    const board = this.board();
    if (!board) return;
    this.dialog.open(ListSettingsDialogComponent, { data: { list } as ListSettingsDialogData })
      .afterClosed().subscribe((settings?: ListSettings) => {
        if (!settings) return;
        this.boardsService.updateList(board.id, list.id, settings).subscribe({
          next: () => this.reloadBoard(),
          error: err => this.boardError.set(err.error?.detail ?? 'Could not update the list.')
        });
      });
  }

  deleteList(list: BoardListDto) {
    const board = this.board();
    if (!board) return;
    if (board.lists.length <= 1) {
      this.boardError.set('A board needs at least one list.');
      return;
    }
    const data: DeleteListDialogData = { list, otherLists: board.lists.filter(l => l.id !== list.id) };
    this.dialog.open(DeleteListDialogComponent, { data })
      .afterClosed().subscribe((result?: { moveTasksTo: string | null }) => {
        if (!result) return;
        this.boardsService.deleteList(board.id, list.id, result.moveTasksTo).subscribe({
          next: () => {
            this.reloadBoard();
            // Rules triggered by the deleted list were removed server-side; refresh the panel.
            const project = this.project();
            if (project) this.automationsService.getForProject(project.id).subscribe(rules => this.automations.set(rules));
          },
          error: err => this.boardError.set(err.error?.detail ?? 'Could not delete the list.')
        });
      });
  }

  moveList(list: BoardListDto, direction: -1 | 1) {
    const board = this.board();
    if (!board) return;
    const ids = board.lists.map(l => l.id);
    const from = ids.indexOf(list.id);
    const to = from + direction;
    if (to < 0 || to >= ids.length) return;
    [ids[from], ids[to]] = [ids[to], ids[from]];

    // Reorder locally right away so the board feels instant; the server copy follows.
    const reordered = ids.map(id => board.lists.find(l => l.id === id)!);
    this.board.set({ ...board, lists: reordered });
    this.boardsService.reorderLists(board.id, ids).subscribe({
      error: err => {
        this.boardError.set(err.error?.detail ?? 'Could not reorder lists.');
        this.reloadBoard();
      }
    });
  }

  wipState(list: BoardListDto): 'over' | 'at' | 'ok' {
    if (!list.wipLimit) return 'ok';
    if (list.tasks.length > list.wipLimit) return 'over';
    return list.tasks.length === list.wipLimit ? 'at' : 'ok';
  }

  /** Several change signals can arrive together (our own REST call plus its SignalR echo), so collapse them into one refetch. */
  private scheduleReload() {
    clearTimeout(this.reloadTimer);
    this.reloadTimer = setTimeout(() => this.reloadBoard(), 250);
  }

  private removeTaskLocally(taskId: string) {
    const board = this.board();
    if (!board) return;
    for (const list of board.lists) {
      list.tasks = list.tasks.filter(t => t.id !== taskId && t.parentTaskId !== taskId);
    }
    this.board.set({ ...board });
  }

  initials(name: string | null | undefined): string {
    if (!name) return '?';
    return name.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  activeSprint = signal<SprintDto | null>(null);
  sprintFilter = signal<'all' | 'sprint'>('all');

  /** Counts for the stats strip, derived from the board as it's currently loaded. */
  stats = computed(() => {
    const lists = this.board()?.lists ?? [];
    let total = 0, done = 0, overdue = 0, unassigned = 0;
    for (const list of lists) {
      for (const task of list.tasks) {
        total++;
        if (list.isDoneColumn) { done++; continue; }
        if (task.isOverdue) overdue++;
        if (!task.assigneeId) unassigned++;
      }
    }
    return { total, done, open: total - done, overdue, unassigned };
  });

  /** With an active sprint the headline is its points (or tasks, if nothing is estimated); otherwise the whole board. */
  private usesSprintPoints = computed(() => (this.activeSprint()?.totalPoints ?? 0) > 0);
  progressDone = computed(() => {
    const sprint = this.activeSprint();
    if (!sprint) return this.stats().done;
    return this.usesSprintPoints() ? sprint.donePoints : sprint.doneCount;
  });
  progressTotal = computed(() => {
    const sprint = this.activeSprint();
    if (!sprint) return this.stats().total;
    return this.usesSprintPoints() ? sprint.totalPoints : sprint.taskCount;
  });
  progressPercent = computed(() => {
    const total = this.progressTotal();
    return total > 0 ? Math.min(100, (this.progressDone() / total) * 100) : 0;
  });

  sprintDaysLeft = computed(() => {
    const sprint = this.activeSprint();
    if (!sprint) return 0;
    return Math.max(0, Math.ceil((Date.parse(sprint.endDate) - Date.now()) / 86_400_000));
  });

  /**
   * The cards to show in a list. Returns the list's own array when no filter applies, so
   * Angular sees a stable reference; with the sprint filter on, only the active sprint's cards.
   */
  visibleTasks(list: BoardListDto): TaskCardDto[] {
    const sprint = this.activeSprint();
    if (this.sprintFilter() !== 'sprint' || !sprint) return list.tasks;
    return list.tasks.filter(t => t.sprintId === sprint.id);
  }

  async onDrop(event: CdkDragDrop<TaskCardDto[]>, targetList: BoardListDto) {
    const board = this.board();
    if (!board) return;

    const moved = event.item.data as TaskCardDto;
    const sourceList = board.lists.find(l => l.tasks.some(t => t.id === moved.id));
    if (!sourceList) return;

    // The drop index counts only the cards on screen. With the sprint filter on, that's not the
    // card's real place in the list, so anchor on the card it landed in front of instead.
    const visibleWithoutMoved = this.visibleTasks(targetList).filter(t => t.id !== moved.id);
    const anchor = visibleWithoutMoved[event.currentIndex];

    sourceList.tasks = sourceList.tasks.filter(t => t.id !== moved.id);
    const targetTasks = targetList.tasks;
    const insertAt = anchor ? targetTasks.findIndex(t => t.id === anchor.id) : targetTasks.length;
    targetTasks.splice(insertAt, 0, moved);
    this.board.set({ ...board });

    this.tasksService.moveTask(moved.id, targetList.id, insertAt, board.id).subscribe({
      error: () => this.reloadBoard()
    });
  }

  openNewTaskDialog(list: BoardListDto) {
    const ref = this.dialog.open(CreateTaskDialogComponent, { width: '500px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      const project = this.project();
      const board = this.board();
      if (!project || !board) return;

      this.tasksService.createTask({
        projectId: project.id,
        boardId: board.id,
        listId: list.id,
        title: result.title,
        description: result.description,
        type: result.type,
        priority: result.priority
      }).subscribe(task => this.addTaskToList(task, list.id));
    });
  }

  openAutomationDialog() {
    const board = this.board();
    const project = this.project();
    if (!board || !project) return;

    const ref = this.dialog.open(CreateAutomationDialogComponent, {
      width: '480px',
      data: { lists: board.lists, members: this.members() } as AutomationDialogData
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.automationsService.create(project.id, {
        name: result.name,
        triggerType: AutomationTriggerType.TaskMovedToList,
        triggerListId: result.triggerListId,
        actionType: result.actionType,
        actionUserId: result.actionUserId
      }).subscribe(rule => this.automations.update(list => [rule, ...list]));
    });
  }

  toggleAutomation(rule: AutomationRuleDto) {
    const enabled = !rule.isEnabled;
    this.automationsService.toggle(rule.id, enabled).subscribe(() => {
      this.automations.update(list =>
        list.map(r => r.id === rule.id ? { ...r, isEnabled: enabled } : r));
    });
  }

  deleteAutomation(rule: AutomationRuleDto) {
    this.automationsService.delete(rule.id).subscribe(() => {
      this.automations.update(list => list.filter(r => r.id !== rule.id));
    });
  }

  actionLabel(rule: AutomationRuleDto): string {
    return rule.actionType === AutomationActionType.AssignUser
      ? `Assign to ${rule.actionUserName}`
      : `Notify ${rule.actionUserName}`;
  }

  openAiDialog(task: TaskCardDto) {
    const board = this.board();
    if (!board) return;

    const ref = this.dialog.open(TaskAiDialogComponent, {
      width: '520px',
      data: { taskId: task.id, taskTitle: task.title, boardId: board.id } as TaskAiDialogData
    });
    ref.afterClosed().subscribe(changed => {
      // Applying a breakdown creates new task cards; assigning changes an existing one -
      // simplest correct refresh is just re-fetching the board rather than patching both cases locally.
      if (changed) this.reloadBoard();
    });
  }

  private reloadBoard() {
    const project = this.project();
    if (!project) return;
    this.boardsService.getProjectBoards(project.id).subscribe(boards => {
      if (boards.length > 0) this.board.set(boards[0]);
    });
  }

  generateAiSummary() {
    const project = this.project();
    if (!project) return;

    this.aiSummaryLoading.set(true);
    this.aiSummaryError.set(null);
    this.aiService.getProjectSummary(project.id).subscribe({
      next: res => {
        this.aiSummary.set(res.summary);
        this.aiSummaryLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.aiSummaryError.set(
          err.error?.title === 'AI.NotConfigured'
            ? 'AI features need an OpenAI API key configured on the server (OpenAI:ApiKey).'
            : err.error?.detail ?? 'Could not generate a summary right now.'
        );
        this.aiSummaryLoading.set(false);
      }
    });
  }

  private applyRemoteMove(event: any) {
    const board = this.board();
    if (!board) return;

    let movedTask: TaskCardDto | null = null;
    for (const list of board.lists) {
      const idx = list.tasks.findIndex(t => t.id === event.taskId);
      if (idx !== -1) {
        movedTask = list.tasks.splice(idx, 1)[0];
        break;
      }
    }
    if (!movedTask) return;

    const targetList = board.lists.find(l => l.id === event.newListId);
    if (targetList) targetList.tasks.splice(event.newPosition, 0, movedTask);
    this.board.set({ ...board });
  }

  private applyRemoteCreate(task: TaskCardDto) {
    this.addTaskToList(task, this.board()?.lists[0]?.id);
  }

  /** Both the creator's own REST response and the SignalR broadcast it triggers
   *  can deliver the same task, so every insertion path is deduped by task id. */
  private addTaskToList(task: TaskCardDto, listId: string | undefined) {
    const board = this.board();
    if (!board || !listId) return;
    if (board.lists.some(l => l.tasks.some(t => t.id === task.id))) return;

    const targetList = board.lists.find(l => l.id === listId);
    if (targetList) {
      targetList.tasks.push(task);
      this.board.set({ ...board });
    }
  }

  /** Soft tinted chip: color carries the meaning (how urgent), not decoration. */
  priorityColor(priority: string): string {
    return ({
      'Critical': 'bg-tone-crit-bg text-tone-crit-fg border-tone-crit-line',
      'Highest':  'bg-tone-crit-bg text-tone-crit-fg border-tone-crit-line',
      'High':     'bg-tone-high-bg text-tone-high-fg border-tone-high-line',
      'Medium':   'bg-tone-med-bg text-tone-med-fg border-tone-med-line',
      'Low':      'bg-tone-low-bg text-tone-low-fg border-tone-low-line',
      'Lowest':   'bg-sunken text-ink-muted border-line'
    } as Record<string, string>)[priority] ?? 'bg-sunken text-ink-muted border-line';
  }

  priorityLabel(priority: string): string {
    return ({
      'Critical': 'P0 Critical',
      'Highest':  'P0 Highest',
      'High':     'P1 High',
      'Medium':   'P2 Med',
      'Low':      'P3 Low',
      'Lowest':   'P4 Lowest'
    } as Record<string, string>)[priority] ?? priority;
  }

  typeIcon(type: string): string {
    return ({
      'Bug':         'bug_report',
      'Feature':     'star',
      'Story':       'auto_stories',
      'Epic':        'flag',
      'Improvement': 'trending_up'
    } as Record<string, string>)[type] ?? 'task';
  }
}
