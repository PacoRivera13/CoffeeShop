import { HttpErrorResponse } from '@angular/common/http';

export interface DisplayProblem {
  status: number | null;
  title: string;
  detail: string | null;
  errors: string[];
}

/**
 * Turns an API failure (ProblemDetails, validation problems with field errors, or a network error) into text the
 * UI can show. Unknown values give a generic message rather than nothing.
 */
export function toDisplayProblem(error: unknown): DisplayProblem | null {
  if (error === null || error === undefined) return null;

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return { status: 0, title: 'Cannot reach the server', detail: 'Check your connection and try again.', errors: [] };
    }
    return fromBody(error.error, error.status) ?? {
      status: error.status,
      title: error.statusText || 'Request failed',
      detail: null,
      errors: [],
    };
  }

  return fromBody(error, null) ?? { status: null, title: 'Something went wrong', detail: null, errors: [] };
}

function fromBody(body: unknown, status: number | null): DisplayProblem | null {
  if (typeof body !== 'object' || body === null || !('title' in body)) return null;

  const problem = body as { title?: unknown; detail?: unknown; status?: unknown; errors?: unknown };
  const errors =
    typeof problem.errors === 'object' && problem.errors !== null
      ? Object.values(problem.errors as Record<string, unknown>).flatMap((messages) =>
          Array.isArray(messages) ? messages.map(String) : [String(messages)],
        )
      : [];

  return {
    status: typeof problem.status === 'number' ? problem.status : status,
    title: String(problem.title),
    detail: typeof problem.detail === 'string' ? problem.detail : null,
    errors,
  };
}
