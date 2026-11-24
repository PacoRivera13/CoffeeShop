export interface SessionClaims {
  sub: string;
  email: string;
  roles: string[];
  /** Expiry in seconds since the epoch. */
  exp: number;
}

/**
 * Reads the claims of a JWT for display only. Nothing here verifies the signature: the server does, on every request.
 */
export function decodeClaims(token: string): SessionClaims | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) return null;

    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=');
    const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)));
    const claims = JSON.parse(json) as Record<string, unknown>;
    const role = claims['role'];

    return {
      sub: String(claims['sub'] ?? ''),
      email: String(claims['email'] ?? ''),
      roles: Array.isArray(role) ? role.map(String) : role ? [String(role)] : [],
      exp: Number(claims['exp'] ?? 0),
    };
  } catch {
    return null;
  }
}
