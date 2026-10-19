/**
 * SpendWise Frontend — Central API Client
 *
 * A thin, reusable wrapper around the native Fetch API for communicating
 * with the SpendWise backend REST API.
 *
 * Design goals:
 *   - Zero external dependencies (native fetch only)
 *   - Type-safe request/response handling
 *   - Automatic Authorization header attachment from tokenStorage
 *   - Standardized ProblemDetails error parsing → ApiError
 *   - FormData / file upload support for evidence and AI attachments
 *   - AbortSignal / cancellation support
 *   - Request correlation ID (X-Request-Id) sent on every request
 *   - Configurable timeout via VITE_API_TIMEOUT_MS
 *   - No business endpoint paths hard-coded here
 *   - No secrets logged
 *
 * PHASE 1 — FOUNDATION ONLY.
 * No existing business mock implementations have been changed.
 * The API client throws ApiError on failures — callers handle the error.
 */

import { parseApiError, networkError, ApiError } from './apiError';
import { getAccessToken } from './tokenStorage';
import { isRefreshRateLimited } from './authRefresh';

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

/**
 * Returns the configured API base URL.
 * Throws a clear error during development if the env var is missing.
 */
function getApiBaseUrl(): string {
  const url = import.meta.env.VITE_API_BASE_URL;
  if (!url) {
    if (import.meta.env.DEV) {
      console.error(
        '[SpendWise API] VITE_API_BASE_URL is not set.\n' +
          'Create a .env.local file with:\n' +
          '  VITE_API_BASE_URL=https://localhost:7256/api\n' +
          'See .env.example for documentation.'
      );
    }
    // Return empty string — requests will fail gracefully with network_error
    return '';
  }
  // Normalize: strip trailing slash
  return url.endsWith('/') ? url.slice(0, -1) : url;
}

/** Request timeout in ms. Reads from env or falls back to 30 seconds. */
function getTimeoutMs(): number {
  const raw = import.meta.env.VITE_API_TIMEOUT_MS;
  if (raw) {
    const parsed = parseInt(raw, 10);
    if (!isNaN(parsed) && parsed > 0) return parsed;
  }
  return 30_000;
}

// ---------------------------------------------------------------------------
// Correlation ID
// ---------------------------------------------------------------------------

function generateRequestId(): string {
  // Simple RFC 4122 v4 UUID-like ID using crypto.randomUUID if available
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  // Fallback for environments without randomUUID
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

// ---------------------------------------------------------------------------
// Request options
// ---------------------------------------------------------------------------

export interface RequestOptions {
  /**
   * AbortSignal for request cancellation.
   * The client wraps this with an internal timeout signal via AbortSignal.any().
   */
  signal?: AbortSignal;

  /**
   * Custom headers merged with defaults (Authorization, Content-Type, X-Request-Id).
   * Do NOT set Authorization manually — the client handles it from tokenStorage.
   */
  headers?: Record<string, string>;

  /**
   * If true, the request body will NOT have Content-Type: application/json set.
   * Use this when sending FormData (browser sets multipart boundary automatically).
   */
  isFormData?: boolean;

  /**
   * If true, returns the response as a binary Blob instead of parsing JSON.
   * Use for authenticated file downloads (e.g. GET /evidence/{id}).
   */
  isBlob?: boolean;
}

// ---------------------------------------------------------------------------
// Core fetch wrapper
// ---------------------------------------------------------------------------

import { attemptTokenRefresh } from './authRefresh';

/**
 * Build the combined abort signal from caller signal + timeout.
 */
function buildSignal(callerSignal?: AbortSignal): AbortSignal {
  const timeoutSignal = AbortSignal.timeout(getTimeoutMs());

  if (!callerSignal) return timeoutSignal;

  if (typeof AbortSignal.any === 'function') {
    return AbortSignal.any([callerSignal, timeoutSignal]);
  }

  const controller = new AbortController();
  function abort(reason?: unknown) {
    controller.abort(reason);
  }

  callerSignal.addEventListener('abort', () => abort(callerSignal.reason), { once: true });
  timeoutSignal.addEventListener('abort', () => abort(timeoutSignal.reason), { once: true });

  return controller.signal;
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  options: RequestOptions = {}
): Promise<T> {
  const executeRequest = async (isRetry = false): Promise<Response> => {
    const baseUrl = getApiBaseUrl();
    const url = `${baseUrl}${path}`;
    const requestId = generateRequestId();

    const headers: Record<string, string> = {
      'X-Request-Id': requestId,
      ...(options.headers ?? {}),
    };

    const token = getAccessToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    if (body !== undefined && !options.isFormData) {
      headers['Content-Type'] = 'application/json';
    }

    let fetchBody: BodyInit | undefined;
    if (body !== undefined) {
      if (options.isFormData) {
        fetchBody = body as FormData;
      } else {
        fetchBody = JSON.stringify(body);
      }
    }

    const signal = buildSignal(options.signal);
    return await fetch(url, {
      method,
      headers,
      body: fetchBody,
      signal,
      credentials: 'include',
    });
  };

  let response: Response;
  try {
    response = await executeRequest();
  } catch (err) {
    throw networkError(err);
  }

  // Handle 401 auto-refresh
  if (
    response.status === 401 &&
    !path.startsWith('/auth/refresh') &&
    !path.startsWith('/auth/login') &&
    !path.startsWith('/auth/pin/verify') &&
    !path.startsWith('/auth/pin/setup') &&
    !isRefreshRateLimited()
  ) {
    try {
      await attemptTokenRefresh();
      // Retry original request
      response = await executeRequest(true);
    } catch (refreshErr) {
      // If refresh fails, fall through to parseApiError below which will throw the 401
    }
  }

  if (response.ok) {
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    if (options.isBlob) {
      return (await response.blob()) as unknown as T;
    }
    try {
      return (await response.json()) as T;
    } catch {
      return undefined as unknown as T;
    }
  }

  const apiErr: ApiError = await parseApiError(response);
  throw apiErr;
}

// ---------------------------------------------------------------------------
// Public HTTP method helpers
// ---------------------------------------------------------------------------

/** Send a GET request. Returns the parsed response body. */
function get<T>(path: string, options?: RequestOptions): Promise<T> {
  return request<T>('GET', path, undefined, options);
}

/** Send a POST request with a JSON body. Returns the parsed response body. */
function post<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
  return request<T>('POST', path, body, options);
}

/** Send a PUT request with a JSON body. Returns the parsed response body. */
function put<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
  return request<T>('PUT', path, body, options);
}

/** Send a PATCH request with a JSON body. Returns the parsed response body. */
function patch<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
  return request<T>('PATCH', path, body, options);
}

/** Send a DELETE request. Returns the parsed response body (usually void). */
function del<T = void>(path: string, options?: RequestOptions): Promise<T> {
  return request<T>('DELETE', path, undefined, options);
}

/** Send a DELETE request with a JSON body (e.g. DELETE /budgets/{id}, DELETE /goals/{id} with PIN). */
function deleteWithBody<T = void>(path: string, body: unknown, options?: RequestOptions): Promise<T> {
  return request<T>('DELETE', path, body, options);
}

/**
 * Send a multipart/form-data POST request.
 * Use for file uploads: transaction evidence, support attachments, AI file inputs.
 *
 * @param path    API path relative to VITE_API_BASE_URL
 * @param form    FormData instance constructed by the caller
 * @param options Additional options (signal, headers)
 */
function upload<T>(path: string, form: FormData, options?: RequestOptions): Promise<T> {
  return request<T>('POST', path, form, { ...options, isFormData: true });
}

/**
 * Send a multipart/form-data PUT request (for replacing an existing upload).
 */
function uploadPut<T>(path: string, form: FormData, options?: RequestOptions): Promise<T> {
  return request<T>('PUT', path, form, { ...options, isFormData: true });
}

/**
 * Send an authenticated GET request and return the binary Blob.
 * Use for downloading evidence/attachments.
 */
function getBlob(path: string, options?: RequestOptions): Promise<Blob> {
  return request<Blob>('GET', path, undefined, { ...options, isBlob: true });
}

// ---------------------------------------------------------------------------
// Named export
// ---------------------------------------------------------------------------

/**
 * Central SpendWise API client.
 */
export const apiClient = {
  get,
  post,
  put,
  patch,
  delete: del,
  deleteWithBody,
  upload,
  uploadPut,
  getBlob,
} as const;
