import { ApiError, mapApiError } from './ApiError';
import { environment } from '@/config/environment';

type AuthHooks = { getAccessToken: () => string | null; refreshSession: () => Promise<string | null> };
let authHooks: AuthHooks = { getAccessToken: () => null, refreshSession: async () => null };
export function configureApiAuth(hooks: AuthHooks) { authHooks = hooks; }

type RequestOptions = RequestInit & { authenticated?: boolean; retryAfterRefresh?: boolean };

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  if (!environment.isApiConfigured) throw new ApiError('Chưa cấu hình EXPO_PUBLIC_API_BASE_URL.', 'configuration');
  const { authenticated = false, retryAfterRefresh = true, ...requestInit } = options;
  const headers = new Headers(requestInit.headers);
  if (requestInit.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (authenticated) {
    const token = authHooks.getAccessToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
  }
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), environment.requestTimeoutMs);
  try {
    const response = await fetch(`${environment.apiBaseUrl}${path}`, { ...requestInit, headers, signal: controller.signal });
    if (response.status === 401 && authenticated && retryAfterRefresh) {
      const replacement = await authHooks.refreshSession();
      if (replacement) return apiRequest<T>(path, { ...options, retryAfterRefresh: false });
    }
    if (!response.ok) throw await mapApiError(response);
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    if (error instanceof Error && error.name === 'AbortError') throw new ApiError('Kết nối đến máy chủ đã hết thời gian chờ.', 'timeout');
    throw new ApiError('Không thể kết nối đến SpendWise API. Hãy kiểm tra URL và mạng.', 'network');
  } finally { clearTimeout(timeout); }
}
