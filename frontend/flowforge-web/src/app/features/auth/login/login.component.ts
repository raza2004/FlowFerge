import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';

interface AuthError {
  message: string;
  hint?: string;
  field?: 'email' | 'password';
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule
  ],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  isLoading = false;
  showPassword = false;
  authError = signal<AuthError | null>(null);

  submit() {
    if (this.form.invalid) return;
    this.isLoading = true;
    this.authError.set(null);

    this.auth.login({
      email: this.form.value.email!,
      password: this.form.value.password!
    }).subscribe({
      next: () => this.router.navigate(['/projects']),
      error: (err: HttpErrorResponse) => {
        this.authError.set(this.parseError(err));
        this.isLoading = false;
      }
    });
  }

  dismissError() {
    this.authError.set(null);
  }

  private parseError(err: HttpErrorResponse): AuthError {
    const code: string = err.error?.errors?.code ?? '';

    switch (err.status) {
      case 401:
        return {
          message: 'Incorrect password.',
          hint: 'Please check your password and try again.',
          field: 'password'
        };
      case 404:
        return {
          message: 'No account found with this email.',
          hint: 'Double-check your email or sign up for a new account.',
          field: 'email'
        };
      case 423:
        return {
          message: 'Account temporarily locked.',
          hint: 'Too many failed attempts. Try again in 15 minutes.'
        };
      case 400:
        return {
          message: 'Invalid request.',
          hint: err.error?.detail ?? 'Please enter a valid email and password.'
        };
      default:
        return {
          message: 'Something went wrong.',
          hint: 'Please try again in a moment.'
        };
    }
  }
}
