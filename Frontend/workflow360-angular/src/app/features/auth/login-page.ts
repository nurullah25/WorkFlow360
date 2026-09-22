import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, isDevMode, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { getErrorMessage } from '../../core/http/api-error';

const DEMO_PASSWORD = 'Passw0rd!';

@Component({
  selector: 'app-login-page',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected readonly showDemoAccounts = isDevMode();
  protected readonly demoAccounts = [
    { email: 'admin@workflow360.local', role: 'Admin' },
    { email: 'farhana.rahman@workflow360.local', role: 'HR' },
    { email: 'tanvir.ahmed@workflow360.local', role: 'Manager' },
    { email: 'rafiq.hasan@workflow360.local', role: 'Employee' },
  ];

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth
      .login(email, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
          this.router.navigateByUrl(returnUrl);
        },
        error: (error: HttpErrorResponse) => {
          // Other statuses (offline, rate limited, server error) are already shown as a toast.
          if (error.status === 400 || error.status === 401) {
            this.errorMessage.set(getErrorMessage(error));
          }
        },
      });
  }

  protected useDemoAccount(email: string): void {
    this.form.setValue({ email, password: DEMO_PASSWORD });
    this.errorMessage.set(null);
  }
}
