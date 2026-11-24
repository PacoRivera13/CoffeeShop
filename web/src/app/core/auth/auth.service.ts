import { computed, inject, Injectable, signal } from '@angular/core';
import { Api } from '../../api/api';
import { login } from '../../api/fn/auth/login';
import { register } from '../../api/fn/auth/register';
import { decodeClaims } from './jwt';

export const SESSION_STORAGE_KEY = 'roastery.session';
export const ADMIN_ROLE = 'Admin';

/** Holds the access token for this browser tab. Roles are read for the UI only; the API enforces them. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(Api);
  private readonly accessToken = signal<string | null>(readStoredToken());

  readonly claims = computed(() => {
    const token = this.accessToken();
    return token ? decodeClaims(token) : null;
  });
  readonly isAuthenticated = computed(() => {
    const claims = this.claims();
    return claims !== null && claims.exp * 1000 > Date.now();
  });
  readonly isAdmin = computed(() => this.isAuthenticated() && this.claims()!.roles.includes(ADMIN_ROLE));
  readonly email = computed(() => (this.isAuthenticated() ? this.claims()!.email : null));

  /** The token to send, or null once it is missing or expired. */
  token(): string | null {
    return this.isAuthenticated() ? this.accessToken() : null;
  }

  /** Where a user lands after signing in. */
  homeUrl(): string {
    return this.isAdmin() ? '/admin' : '/';
  }

  async login(email: string, password: string): Promise<void> {
    const response = await this.api.invoke(login, { body: { email, password } });
    this.store(response.accessToken);
  }

  async register(name: string, email: string, password: string): Promise<void> {
    await this.api.invoke(register, { body: { name, email, password } });
    await this.login(email, password);
  }

  logout(): void {
    this.store(null);
  }

  private store(token: string | null): void {
    this.accessToken.set(token);
    try {
      if (token) sessionStorage.setItem(SESSION_STORAGE_KEY, token);
      else sessionStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // Without storage the session simply ends with the tab.
    }
  }
}

function readStoredToken(): string | null {
  try {
    return sessionStorage.getItem(SESSION_STORAGE_KEY);
  } catch {
    return null;
  }
}
