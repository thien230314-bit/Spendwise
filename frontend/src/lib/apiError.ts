/**
 * SpendWise Frontend — Normalized API Error Model
 *
 * Provides a frontend-safe error type that covers all expected backend response
 * error categories without leaking server internals.
 *
 * Backend is expected to return RFC 9457 / RFC 7807 ProblemDetails:
 *   {
 *     type?: string;      // URI reference (optional)
 *     title: string;      // human-readable summary
 *     status: number;     // HTTP status code
 *     detail?: string;    // extended description
 *     instance?: string;  // URI of affected resource
 *     errors?: Record<string, string[]>; // validation field errors (400)
 *     code?: string;      // business error code (optional, SpendWise extension)
 *     traceId?: string;   // correlation ID (optional, SpendWise extension)
 *   }
 */

// ---------------------------------------------------------------------------
// HTTP Status Categories
// ---------------------------------------------------------------------------

export type ApiErrorKind =
  | 'validation'          // 400 — request payload validation failed
  | 'unauthorized'        // 401 — missing or invalid/expired token
  | 'forbidden'           // 403 — authenticated but not authorized for resource
  | 'not_found'           // 404 — resource does not exist
  | 'conflict'            // 409 — state conflict (duplicate, concurrent update)
  | 'semantic_error'      // 422 — request is well-formed but semantically invalid
  | 'rate_limited'        // 429 — too many requests
  | 'maintenance'         // 503 — backend / system maintenance mode
  | 'ai_unavailable'      // 503 — AI provider temporarily unavailable
  | 'server_error'        // 500/502/504 — unexpected backend error
  | 'network_error'       // fetch failed / backend unreachable
  | 'unknown';            // any other unclassified error

// ---------------------------------------------------------------------------
// Normalized Error Type
// ---------------------------------------------------------------------------

/**
 * ApiError is the single normalized error representation used everywhere
 * in the frontend after an API call fails.
 *
 * Consumers should switch on `kind` to decide UX behavior, and use
 * `code` for backend-specific business error differentiation.
 */
export interface ApiError {
  /** High-level category for routing UI decisions. */
  kind: ApiErrorKind;

  /** HTTP status code, or 0 for network/unreachable errors. */
  status: number;

  /**
   * Human-readable summary from backend `title` field, or a generic
   * frontend fallback. Safe to display in UI.
   */
  title: string;

  /**
   * Optional extended description from backend `detail` field.
   * May or may not be safe to show directly — prefer translated messages.
   */
  detail?: string;

  /**
   * Opaque business error code from backend extension field `code`.
   * Example: "ACCOUNT_LOCKED", "INVALID_PIN", "BUDGET_EXCEEDED"
   * Use this for fine-grained UI differentiation, not `title` strings.
   */
  code?: string;

  /**
   * Field-level validation errors from backend `errors` map.
   * Keys are field names (may use dotted path), values are error message arrays.
   * Only populated for `kind === 'validation'`.
   */
  fieldErrors?: Record<string, string[]>;

  /**
   * Raw value of Retry-After header (if present).
   */
  retryAfterHeader?: string;

  /**
   * Normalized cooldown duration in seconds parsed from Retry-After header
   * (supports both integer seconds and HTTP-date formats).
   */
  retryAfterSeconds?: number;

  /**
   * Correlation/trace ID from backend `traceId` or `X-Trace-Id` header.
   * Include in support reports. Never log sensitive user data alongside this.
   */
  traceId?: string;

  /**
   * The raw response body as parsed JSON (if parsing succeeded).
   * Available for debugging but should NOT be displayed directly in UI.
   */
  rawBody?: unknown;
}

// ---------------------------------------------------------------------------
// Raw ProblemDetails shape (backend contract)
// ---------------------------------------------------------------------------

interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  code?: string;
  traceId?: string;
  [key: string]: unknown;
}

// ---------------------------------------------------------------------------
// Kind classifier
// ---------------------------------------------------------------------------

function classifyStatus(status: number, code?: string): ApiErrorKind {
  switch (status) {
    case 400: return 'validation';
    case 401: return 'unauthorized';
    case 403: return 'forbidden';
    case 404: return 'not_found';
    case 409: return 'conflict';
    case 422: return 'semantic_error';
    case 429: return 'rate_limited';
    case 503:
      // Distinguish maintenance vs AI provider unavailable via backend code
      if (code === 'AI_PROVIDER_UNAVAILABLE' || code === 'AI_UNAVAILABLE') {
        return 'ai_unavailable';
      }
      return 'maintenance';
    default:
      if (status >= 500) return 'server_error';
      if (status >= 400) return 'unknown';
      return 'unknown';
  }
}

// ---------------------------------------------------------------------------
// Parsers
// ---------------------------------------------------------------------------

/**
 * Parse a backend HTTP response into a normalized ApiError.
 * Call this only when `response.ok === false`.
 */
export async function parseApiError(response: Response): Promise<ApiError> {
  const status = response.status;
  const traceId = response.headers.get('X-Trace-Id') ?? undefined;

  // Extract and normalize Retry-After header (supports delay-seconds and HTTP-date)
  const retryAfterHeader = response.headers.get('Retry-After') ?? undefined;
  let retryAfterSeconds: number | undefined = undefined;

  if (retryAfterHeader) {
    const parsedSec = parseInt(retryAfterHeader, 10);
    if (!isNaN(parsedSec) && parsedSec >= 0) {
      retryAfterSeconds = parsedSec;
    } else {
      const parsedDateMs = new Date(retryAfterHeader).getTime();
      if (!isNaN(parsedDateMs) && parsedDateMs > Date.now()) {
        retryAfterSeconds = Math.ceil((parsedDateMs - Date.now()) / 1000);
      }
    }
  }

  let problem: ProblemDetails | null = null;

  try {
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/problem+json') || contentType.includes('application/json')) {
      problem = (await response.json()) as ProblemDetails;
    }
  } catch {
    // Body could not be parsed as JSON — treat as opaque server error
  }

  const code = problem?.code;
  const kind = classifyStatus(status, code);

  return {
    kind,
    status,
    title: problem?.title ?? defaultTitle(kind),
    detail: problem?.detail,
    code,
    fieldErrors: problem?.errors,
    retryAfterHeader,
    retryAfterSeconds,
    traceId: problem?.traceId ?? traceId,
    rawBody: problem ?? undefined,
  };
}

/**
 * Create an ApiError from a network-level failure (fetch threw, e.g., no connection).
 */
export function networkError(cause?: unknown): ApiError {
  return {
    kind: 'network_error',
    status: 0,
    title: 'Network error — backend unreachable',
    detail: cause instanceof Error ? cause.message : undefined,
  };
}

/**
 * Type guard: check whether a thrown value is a normalized ApiError.
 */
export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' &&
    value !== null &&
    'kind' in value &&
    'status' in value &&
    'title' in value
  );
}

// ---------------------------------------------------------------------------
// Generic title fallbacks (used when backend provides no `title`)
// ---------------------------------------------------------------------------

function defaultTitle(kind: ApiErrorKind): string {
  switch (kind) {
    case 'validation':    return 'Validation failed';
    case 'unauthorized':  return 'Authentication required';
    case 'forbidden':     return 'Access denied';
    case 'not_found':     return 'Resource not found';
    case 'conflict':      return 'Conflict — state cannot be applied';
    case 'semantic_error':return 'Request could not be processed';
    case 'rate_limited':  return 'Too many requests — please slow down';
    case 'maintenance':   return 'Service temporarily unavailable';
    case 'ai_unavailable':return 'AI service temporarily unavailable';
    case 'server_error':  return 'Unexpected server error';
    case 'network_error': return 'Network error — backend unreachable';
    default:              return 'An unexpected error occurred';
  }
}
