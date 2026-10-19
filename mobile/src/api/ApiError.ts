export type ApiErrorKind = 'configuration' | 'network' | 'timeout' | 'unauthorized' | 'forbidden' | 'validation' | 'server';

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly kind: ApiErrorKind,
    public readonly status?: number,
    public readonly code?: string,
    public readonly validationErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

type ProblemDetails = { title?: string; detail?: string; code?: string; errors?: Record<string, string[]> };

export async function mapApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};
  try { problem = (await response.json()) as ProblemDetails; } catch { /* Body can be empty. */ }
  const message = problem.detail ?? problem.title ?? `Yêu cầu thất bại (HTTP ${response.status}).`;
  if (response.status === 401) return new ApiError(message, 'unauthorized', response.status, problem.code);
  if (response.status === 403) return new ApiError(message, 'forbidden', response.status, problem.code);
  if (response.status === 400 || response.status === 422) return new ApiError(message, 'validation', response.status, problem.code, problem.errors);
  return new ApiError(message, 'server', response.status, problem.code);
}
