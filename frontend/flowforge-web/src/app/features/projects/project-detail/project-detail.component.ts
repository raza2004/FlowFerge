import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDialogRef } from '@angular/material/dialog';
import {
  CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem
} from '@angular/cdk/drag-drop';
import { ProjectsService } from '../../../shared/services/projects.service';
import { BoardsService } from '../../../shared/services/boards.service';
import { TasksService } from '../../../shared/services/tasks.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { ProjectDto, BoardDto, BoardListDto, TaskCardDto } from '../../../shared/models/project.models';

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

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink, DragDropModule,
    MatCardModule, MatIconModule, MatButtonModule, MatChipsModule,
    MatMenuModule, MatDialogModule
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss'
})
export class ProjectDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private projectsService = inject(ProjectsService);
  private boardsService = inject(BoardsService);
  private tasksService = inject(TasksService);
  private signalr = inject(SignalrService);
  private dialog = inject(MatDialog);

  project = signal<ProjectDto | null>(null);
  board = signal<BoardDto | null>(null);
  isLoading = signal(true);

  get listConnectedTo(): string[] {
    return this.board()?.lists.map(l => `list-${l.id}`) ?? [];
  }

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.projectsService.getById(id).subscribe(p => this.project.set(p));

    this.boardsService.getProjectBoards(id).subscribe(async boards => {
      if (boards.length > 0) {
        this.board.set(boards[0]);
        await this.signalr.startConnection();
        await this.signalr.joinBoard(boards[0].id);

        this.signalr.taskMoved$.subscribe(event => this.applyRemoteMove(event));
        this.signalr.taskCreated$.subscribe(task => this.applyRemoteCreate(task));
      }
      this.isLoading.set(false);
    });
  }

  async ngOnDestroy() {
    const b = this.board();
    if (b) await this.signalr.leaveBoard(b.id);
  }

  async onDrop(event: CdkDragDrop<TaskCardDto[]>, targetList: BoardListDto) {
    const board = this.board();
    if (!board) return;

    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(event.previousContainer.data, event.container.data,
        event.previousIndex, event.currentIndex);
    }

    const movedTask = event.container.data[event.currentIndex];
    this.tasksService.moveTask(movedTask.id, targetList.id, event.currentIndex, board.id).subscribe();
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
      }).subscribe(task => {
        const updatedBoard = { ...board };
        const targetList = updatedBoard.lists.find(l => l.id === list.id);
        if (targetList) targetList.tasks.push(task);
        this.board.set(updatedBoard);
      });
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
    const board = this.board();
    if (!board) return;
    if (board.lists.some(l => l.tasks.some(t => t.id === task.id))) return;
    const list = board.lists[0];
    if (list) {
      list.tasks.push(task);
      this.board.set({ ...board });
    }
  }

  priorityColor(priority: string): string {
    return ({
      'Critical': 'bg-red-100 text-red-800',
      'Highest':  'bg-red-100 text-red-800',
      'High':     'bg-orange-100 text-orange-800',
      'Medium':   'bg-yellow-100 text-yellow-800',
      'Low':      'bg-blue-100 text-blue-800',
      'Lowest':   'bg-gray-100 text-gray-800'
    } as Record<string, string>)[priority] ?? 'bg-gray-100 text-gray-800';
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
