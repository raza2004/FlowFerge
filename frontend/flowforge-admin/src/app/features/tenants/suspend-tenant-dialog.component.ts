import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-suspend-tenant-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Suspend workspace</h2>
    <mat-dialog-content>
      <p class="text-sm text-zinc-500 mb-3">Members of this workspace will be unable to sign into it until it's reactivated.</p>
      <mat-form-field appearance="outline" class="w-full">
        <mat-label>Reason</mat-label>
        <textarea matInput [formControl]="reason" rows="3" placeholder="e.g. Billing overdue"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-raised-button color="warn" [disabled]="reason.invalid" (click)="ref.close(reason.value)">Suspend</button>
    </mat-dialog-actions>
  `
})
export class SuspendTenantDialogComponent {
  ref = inject(MatDialogRef<SuspendTenantDialogComponent>);
  reason = inject(FormBuilder).control('', Validators.required);
}
