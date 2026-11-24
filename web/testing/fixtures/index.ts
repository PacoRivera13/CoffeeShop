// Typed API payloads for tests, built from the models generated from openapi.json, so a contract change breaks
// the tests at compile time instead of letting them mock a response the API no longer sends.
import type { HttpValidationProblemDetails } from '../../src/app/api/models/http-validation-problem-details';
import type { LoginResponse } from '../../src/app/api/models/login-response';
import type { ProblemDetails } from '../../src/app/api/models/problem-details';
import type { RegisterResponse } from '../../src/app/api/models/register-response';

export type Role = 'Customer' | 'Admin';

export const customer = { email: 'ana@example.test', password: 'Str0ng!Passw0rd', name: 'Ana López' } as const;
export const admin = { email: 'admin@example.test', password: 'Adm1n!Passw0rd', name: 'Admin' } as const;

/** An unsigned JWT with the claims the UI reads; the API is mocked, so no signature is checked. */
export function fakeAccessToken(role: Role, email: string = role === 'Admin' ? admin.email : customer.email): string {
  const encode = (value: object) => Buffer.from(JSON.stringify(value)).toString('base64url');
  const exp = Math.floor(Date.now() / 1000) + 3600;
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: `user-${role}`, email, role, exp })}.signature`;
}

export function loginResponse(role: Role, email?: string): LoginResponse {
  return { accessToken: fakeAccessToken(role, email), tokenType: 'Bearer', expiresIn: 3600 };
}

export const registerResponse: RegisterResponse = { customerId: 42, email: customer.email };

export const invalidCredentials: ProblemDetails = { status: 401, title: 'Invalid credentials' };

export const duplicateEmail: ProblemDetails = { status: 409, title: 'Email already registered' };

export const weakPassword: HttpValidationProblemDetails = {
  status: 400,
  title: 'One or more validation errors occurred.',
  errors: { password: ["Passwords must have at least one non alphanumeric character."] },
};
