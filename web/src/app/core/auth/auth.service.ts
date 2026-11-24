import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
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
  private readonly accessToken = signal<string | null>(null);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly claims = computed(() => {
    const token = this.accessToken();
    return token ? decodeClaims(token) : null;
  });
  // Expiry is enforced by the timer in store(), which clears the token when it lapses; a computed cannot
  // react to the clock on its own.
  readonly isAuthenticated = computed(() => this.claims() !== null);
  readonly isAdmin = computed(() => this.isAuthenticated() && this.claims()!.roles.includes(ADMIN_ROLE));
  readonly email = computed(() => (this.isAuthenticated() ? this.claims()!.email : null));

  constructor() {
    this.store(readStoredToken());
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer));
  }

  /** The token to send, or null when signed out. */
  token(): string | null {
    return this.accessToken();
  }

  /** Where a user lands after signing in. */
  homeUrl(): string {
    return this.isAdmin() ? '/admin' : '/';
  }

  async login(email: string, password: string): Promise<void> {
    const response = await this.api.invoke(login, { body: { email, password } });
    this.store(response.accessToken);
  }

  /** Creates the account only; signing in is a separate step, so a failure there never hides a created account. */
  async register(name: string, email: string, password: string): Promise<void> {
    await this.api.invoke(register, { body: { name, email, password } });
  }

  logout(): void {
    this.store(null);
  }

  private store(token: string | null): void {
    clearTimeout(this.expiryTimer);

    const claims = token ? decodeClaims(token) : null;
    const msLeft = claims ? claims.exp * 1000 - Date.now() : 0;
    const valid = token !== null && claims !== null && msLeft > 0;

    this.accessToken.set(valid ? token : null);
    if (valid) this.expiryTimer = setTimeout(() => this.store(null), msLeft);

    try {
      if (valid) sessionStorage.setItem(SESSION_STORAGE_KEY, token);
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
