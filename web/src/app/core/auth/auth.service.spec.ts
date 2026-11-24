import { TestBed } from '@angular/core/testing';
import { Api } from '../../api/api';
import { AuthService, SESSION_STORAGE_KEY } from './auth.service';

function token(role: string, expiresInSeconds: number): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const exp = Math.floor(Date.now() / 1000) + expiresInSeconds;
  return `${encode({ alg: 'HS256' })}.${encode({ sub: 'u1', email: 'ana@example.test', role, exp })}.sig`;
}

describe('AuthService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: { invoke: vi.fn() } }] });
  });

  afterEach(() => {
    sessionStorage.removeItem(SESSION_STORAGE_KEY);
    vi.useRealTimers();
  });

  it('restores a valid session from storage and reads its role', () => {
    sessionStorage.setItem(SESSION_STORAGE_KEY, token('Admin', 3600));

    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.isAdmin()).toBe(true);
    expect(auth.homeUrl()).toBe('/admin');
  });

  it('signs out when the token expires, without any request', () => {
    vi.useFakeTimers();
    sessionStorage.setItem(SESSION_STORAGE_KEY, token('Customer', 60));
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBe(true);

    vi.advanceTimersByTime(61_000);

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('discards an already expired stored token', () => {
    sessionStorage.setItem(SESSION_STORAGE_KEY, token('Customer', -10));

    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });
});
