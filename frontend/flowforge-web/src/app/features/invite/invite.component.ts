import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../core/services/auth.service';
import { InvitationPreviewDto } from '../../shared/models/auth.models';

@Component({
  selector: 'app-invite',
  standalone: true,
  imports: [
    CommonModule, RouterLink, ReactiveFormsModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule
  ],
  templateUrl: './invite.component.html'
})
export class InviteComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  auth = inject(AuthService);

  token = '';
  preview = signal<InvitationPreviewDto | null>(null);
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  showPassword = false;

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.minLength(8),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/)]]
  });

  signedInAsInvitee = computed(() =>
    !!this.preview() && this.auth.user()?.email?.toLowerCase() === this.preview()!.email.toLowerCase());

  ngOnInit() {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.auth.getInvitationPreview(this.token).subscribe({
      next: p => {
        this.preview.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set("This invitation link isn't valid. Ask whoever invited you to send a new one.");
        this.loading.set(false);
      }
    });
  }

  accept() {
    this.busy.set(true);
    this.error.set(null);
    this.auth.acceptInvitation(this.token).subscribe({
      next: () => this.enterApp(),
      error: (err: HttpErrorResponse) => this.fail(err, 'Could not accept the invitation.')
    });
  }

  register() {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.registerWithInvitation(this.token, v.firstName!, v.lastName!, v.password!).subscribe({
      next: () => this.enterApp(),
      error: (err: HttpErrorResponse) => this.fail(err, 'Could not create your account.')
    });
  }

  signInToAccept() {
    this.router.navigate(['/auth/login'], { queryParams: { returnUrl: `/invite/${this.token}` } });
  }

  switchAccount() {
    this.auth.clearSession();
  }

  /** Full reload so realtime connections and cached state start fresh in the new workspace. */
  private enterApp() {
    window.location.assign('/projects');
  }

  private fail(err: HttpErrorResponse, fallback: string) {
    this.busy.set(false);
    this.error.set(err.error?.detail ?? fallback);
  }
}
