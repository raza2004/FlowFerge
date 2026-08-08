import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
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
import { ProjectSummaryDto } from '../../../shared/models/project.models';

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
      <form [formGroup]="form" class="space-y-3 pt-2">
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Project name</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Key (e.g. PROJ)</mat-label>
          <input matInput formControlName="key" style="text-transform:uppercase">
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
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="create()">Create</button>
    </mat-dialog-actions>
  `
})
export class CreateProjectDialogComponent {
  private fb = inject(FormBuilder);
  ref = inject(MatDialogRef<CreateProjectDialogComponent>);

  form = this.fb.group({
    name: ['', Validators.required],
    key: ['', [Validators.required, Validators.maxLength(10)]],
    description: [''],
    visibility: [0, Validators.required]
  });

  create() { this.ref.close(this.form.value); }
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
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.projectsService.create({
        name: result.name,
        key: result.key.toUpperCase(),
        description: result.description,
        color: '#6366f1',
        visibility: result.visibility
      }).subscribe(p => this.projects.update(list => [...list, {
        id: p.id, name: p.name, key: p.key, color: p.color,
        status: p.status, openTasks: 0, completedTasks: 0
      }]));
    });
  }
}
