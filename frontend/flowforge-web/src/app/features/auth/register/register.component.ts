import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
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
  field?: 'email' | 'password' | 'general';
}

function passwordStrengthValidator(control: AbstractControl): ValidationErrors | null {
  const v: string = control.value ?? '';
  const errors: ValidationErrors = {};
  if (v.length < 8)          errors['minLength']  = true;
  if (!/[A-Z]/.test(v))      errors['uppercase']  = true;
  if (!/[a-z]/.test(v))      errors['lowercase']  = true;
  if (!/[0-9]/.test(v))      errors['number']     = true;
  return Object.keys(errors).length ? errors : null;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule
  ],
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  form = this.fb.group({
    firstName:  ['', Validators.required],
    lastName:   ['', Validators.required],
    email:      ['', [Validators.required, Validators.email]],
    password:   ['', [Validators.required, passwordStrengthValidator]],
    tenantName: ['', Validators.required]
  });

  isLoading = false;
  showPassword = false;
  authError = signal<AuthError | null>(null);

  get pwd() { return this.form.controls.password; }

  get rules() {
    const v: string = this.pwd.value ?? '';
    return {
      minLength: v.length >= 8,
      uppercase: /[A-Z]/.test(v),
      lowercase: /[a-z]/.test(v),
      number:    /[0-9]/.test(v)
    };
  }

  get showChecklist(): boolean {
    return (this.pwd.value?.length ?? 0) > 0 && this.pwd.invalid;
  }

  private toSlug(name: string): string {
    return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  }

  submit() {
    if (this.form.invalid) return;
    this.isLoading = true;
    this.authError.set(null);

    const tenantName = this.form.value.tenantName!;

    this.auth.register({
      firstName:  this.form.value.firstName!,
      lastName:   this.form.value.lastName!,
      email:      this.form.value.email!,
      password:   this.form.value.password!,
      tenantName,
      tenantSlug: this.toSlug(tenantName)
    }).subscribe({
      next: () => this.router.navigate(['/projects']),
      error: (err: HttpErrorResponse) => {
        this.authError.set(this.parseError(err));
        this.isLoading = false;
      }
    });
  }

  dismissError() { this.authError.set(null); }

  private parseError(err: HttpErrorResponse): AuthError {
    switch (err.status) {
      case 409:
        return {
          message: 'Email already registered.',
          hint: 'An account with this email already exists. Try signing in instead.',
          field: 'email'
        };
      case 400:
        return {
          message: 'Invalid details.',
          hint: err.error?.detail || 'Please check your information and try again.',
          field: 'general'
        };
      default:
        return { message: 'Something went wrong.', hint: 'Please try again in a moment.' };
    }
  }
}
