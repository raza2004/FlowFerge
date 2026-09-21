import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDialogRef } from '@angular/material/dialog';
import { ProjectsService } from '../../../shared/services/projects.service';
import { ProjectDto, ProjectSummaryDto } from '../../../shared/models/project.models';

@Component({
  selector: 'app-create-project-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>New project</h2>
    <mat-dialog-content>
      @if (error()) {
        <div class="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {{ error() }}
        </div>
      }
      <form [formGroup]="form" class="space-y-3 pt-2">
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Project name</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Key (e.g. PROJ)</mat-label>
          <input matInput formControlName="key" style="text-transform:uppercase">
          <mat-hint>2-10 characters, starting with a letter (letters and numbers only)</mat-hint>
          @if (form.controls.key.touched && form.controls.key.hasError('pattern')) {
            <mat-error>Must start with a letter and contain only letters/numbers</mat-error>
          }
          @if (form.controls.key.touched && form.controls.key.hasError('minlength')) {
            <mat-error>Must be at least 2 characters</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="2"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Visibility</mat-label>
          <mat-select formControlName="visibility">
            <mat-option [value]="0">Private</mat-option>
            <mat-option [value]="1">Internal</mat-option>
            <mat-option [value]="2">Public</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid || isSaving()" (click)="create()">
        {{ isSaving() ? 'Creating…' : 'Create' }}
      </button>
    </mat-dialog-actions>
  `
})
export class CreateProjectDialogComponent {
  private fb = inject(FormBuilder);
  private projectsService = inject(ProjectsService);
  ref = inject(MatDialogRef<CreateProjectDialogComponent>);

  isSaving = signal(false);
  error = signal<string | null>(null);

  form = this.fb.group({
    name: ['', Validators.required],
    key: ['', [Validators.required, Validators.pattern(/^[A-Za-z][A-Za-z0-9]*$/), Validators.minLength(2), Validators.maxLength(10)]],
    description: [''],
    visibility: [0, Validators.required]
  });

  create() {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    this.error.set(null);
    const value = this.form.value;

    this.projectsService.create({
      name: value.name!,
      key: value.key!.toUpperCase(),
      description: value.description ?? undefined,
      color: '#6366f1',
      visibility: value.visibility!
    }).subscribe({
      next: project => this.ref.close(project),
      error: (err: HttpErrorResponse) => {
        this.isSaving.set(false);
        this.error.set(err.error?.detail ?? 'Failed to create project. Please try again.');
      }
    });
  }
}

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatCardModule, MatIconModule, MatButtonModule, MatDialogModule
  ],
  templateUrl: './project-list.component.html'
})
export class ProjectListComponent implements OnInit {
  private projectsService = inject(ProjectsService);
  private dialog = inject(MatDialog);

  projects = signal<ProjectSummaryDto[]>([]);
  isLoading = signal(true);

  ngOnInit() {
    this.projectsService.getAll().subscribe({
      next: p => { this.projects.set(p); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }

  openCreateDialog() {
    const ref = this.dialog.open(CreateProjectDialogComponent, { width: '480px' });
    ref.afterClosed().subscribe((project?: ProjectDto) => {
      if (!project) return;
      this.projects.update(list => [...list, {
        id: project.id, name: project.name, key: project.key, color: project.color,
        status: project.status, openTasks: 0, completedTasks: 0
      }]);
    });
  }
}
