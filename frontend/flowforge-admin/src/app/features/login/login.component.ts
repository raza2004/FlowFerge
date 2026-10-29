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
    <div class="min-h-screen bg-sunken flex items-center justify-center p-4">
      <div class="w-full max-w-md">
        <div class="mb-8">
          <div class="flex items-baseline gap-1">
            <span class="font-display text-4xl leading-none">FlowForge</span>
            <span class="w-1.5 h-1.5 rounded-full bg-ember-500"></span>
          </div>
          <div class="mt-3 inline-flex font-mono text-[10px] uppercase tracking-[0.18em] text-ember-600 border border-ember-500/50 rounded-sm px-2 py-0.5">Admin console</div>
          <h1 class="font-display text-5xl text-ink mt-8 leading-none">System administration</h1>
          <p class="text-ink-muted mt-3 text-sm">Sign in with a system admin account</p>
        </div>

        <div class="bg-panel rounded-xl border border-line-strong p-8">
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

          <p class="text-center text-xs text-ink-muted mt-6 leading-relaxed">
            There's no signup here. This app is for admins only.
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
