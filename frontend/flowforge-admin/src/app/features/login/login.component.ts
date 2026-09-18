import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule
  ],
  template: `
    <div class="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div class="w-full max-w-sm">
        <div class="text-center mb-8">
          <div class="inline-flex items-center gap-2 mb-4">
            <div class="w-10 h-10 bg-admin-900 rounded-xl flex items-center justify-center">
              <mat-icon class="text-white">shield</mat-icon>
            </div>
            <span class="text-2xl font-bold text-gray-900">FlowForge Admin</span>
          </div>
          <h1 class="text-xl font-semibold text-gray-900">System administration</h1>
          <p class="text-gray-500 mt-1 text-sm">Sign in with a system admin account</p>
        </div>

        <div class="bg-white rounded-2xl shadow-sm border border-gray-200 p-8">
          @if (error()) {
            <div class="mb-5 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              {{ error() }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="email">
            </mat-form-field>
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="current-password">
            </mat-form-field>

            <button mat-raised-button color="primary" class="w-full !py-3"
                    type="submit" [disabled]="form.invalid || isLoading()">
              {{ isLoading() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>

          <p class="text-center text-xs text-gray-500 mt-6 leading-relaxed">
            There's no sign-up here — this app is admin-only.
            The very first account ever registered in FlowForge automatically becomes a
            system admin. If that's not you yet,
            <a [href]="mainAppRegisterUrl" class="text-admin-700 hover:underline font-medium">
              register in the main app
            </a>
            and sign in here with those same credentials.
          </p>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AdminAuthService);
  private router = inject(Router);

  isLoading = signal(false);
  error = signal<string | null>(null);
  mainAppRegisterUrl = `${environment.mainAppUrl}/auth/register`;

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  submit() {
    if (this.form.invalid) return;
    this.isLoading.set(true);
    this.error.set(null);

    this.auth.login({ email: this.form.value.email!, password: this.form.value.password! }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse | Error) => {
        this.isLoading.set(false);
        if (err instanceof Error && err.message === 'NOT_SYSTEM_ADMIN') {
          this.error.set("This account doesn't have system admin access.");
        } else if (err instanceof HttpErrorResponse) {
          this.error.set(err.error?.detail ?? 'Invalid email or password.');
        } else {
          this.error.set('Something went wrong. Please try again.');
        }
      }
    });
  }
}
