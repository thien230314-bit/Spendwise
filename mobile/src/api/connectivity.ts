import { environment } from '@/config/environment';

export type ConnectivityState = 'not-configured' | 'checking' | 'reachable' | 'unreachable';

export async function checkConnectivity(): Promise<ConnectivityState> {
  if (!environment.isApiConfigured) return 'not-configured';
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), environment.requestTimeoutMs);
  try {
    const response = await fetch(`${environment.apiBaseUrl}/health/live`, { signal: controller.signal });
    return response.ok ? 'reachable' : 'unreachable';
  } catch { return 'unreachable'; } finally { clearTimeout(timeout); }
}
