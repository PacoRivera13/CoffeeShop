import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';

/** Mirrors the API's limits; the server still validates (password complexity, duplicates). */
export const NAME_MAX_LENGTH = 200;
export const PASSWORD_MIN_LENGTH = 8;

@Component({
  selector: 'app-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    ErrorBannerComponent,
  ],
  templateUrl: './register-page.component.html',
  styleUrl: './register-page.component.scss',
})
export class RegisterPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      // The pattern rejects names made only of spaces, which the API trims and refuses.
      validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(NAME_MAX_LENGTH)],
    }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)],
    }),
  });
  protected readonly error = signal<unknown>(null);
  protected readonly submitting = signal(false);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { name, email, password } = this.form.getRawValue();
    try {
      await this.auth.register(name.trim(), email, password);
    } catch (error) {
      this.error.set(error);
      this.submitting.set(false);
      return;
    }

    // The account exists now: if signing in fails, send the user to sign in rather than to a register retry
    // that could only answer "Email already registered".
    try {
      await this.auth.login(email, password);
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch {
      await this.router.navigate(['/login'], { queryParams: { registered: 'true' } });
    } finally {
      this.submitting.set(false);
    }
  }
}
