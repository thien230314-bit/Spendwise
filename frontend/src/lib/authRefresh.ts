import { getRefreshToken, saveAccessToken, saveRefreshToken, saveAccessTokenExpiresAt, saveStoredUser, clearTokens } from './tokenStorage';
import { saveDeviceTrustToken } from './deviceTrustStorage';
import type { AuthResponse } from '../services/authApiService';
import { mapUserProfile } from '../services/authMapper';

export interface RefreshError extends Error {
  status?: number;
  retryAfterSeconds?: number;
  isRateLimited?: boolean;
  isNetworkOrServer?: boolean;
}

let refreshPromise: Promise<AuthResponse | null> | null = null;
let sessionExpiredCallback: (() => void) | null = null;
let rateLimitedUntilMs = 0;

export function registerSessionExpiredCallback(cb: () => void): void {
  sessionExpiredCallback = cb;
}

export function clearRefreshPromise(): void {
  refreshPromise = null;
}

/**
 * Check if token refresh is currently in a 429 rate limit cooldown.
 */
export function isRefreshRateLimited(): boolean {
  return Date.now() < rateLimitedUntilMs;
}

/**
 * Get remaining cooldown in seconds (or 0 if not cooling down).
 */
export function getRefreshCooldownRemainingSeconds(): number {
  const diff = rateLimitedUntilMs - Date.now();
  return diff > 0 ? Math.ceil(diff / 1000) : 0;
}

export async function attemptTokenRefresh(): Promise<AuthResponse | null> {
  // If actively cooling down from a previous 429, don't spam the server
  if (Date.now() < rateLimitedUntilMs) {
    const err: RefreshError = new Error('Token refresh rate-limited (cooldown active)');
    err.status = 429;
    err.isRateLimited = true;
    err.retryAfterSeconds = Math.ceil((rateLimitedUntilMs - Date.now()) / 1000);
    throw err;
  }

  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async (): Promise<AuthResponse | null> => {
    const refreshToken = getRefreshToken();
    if (!refreshToken) {
      clearTokens();
      if (sessionExpiredCallback) sessionExpiredCallback();
      const err: RefreshError = new Error('No refresh token available');
      err.status = 401;
      throw err;
    }

    try {
      const baseUrl = import.meta.env.VITE_API_BASE_URL;
      const url = `${baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl}/auth/refresh`;
      
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken }),
      });

      if (!response.ok) {
        const status = response.status;
        const err: RefreshError = new Error(`Refresh failed with HTTP ${status}`);
        err.status = status;

        if (status === 429) {
          err.isRateLimited = true;
          // Parse Retry-After header: can be seconds or HTTP-date
          const retryAfterHeader = response.headers.get('Retry-After') || response.headers.get('retry-after');
          let cooldownSeconds = 15; // default 15s cooldown if unspecified
          if (retryAfterHeader) {
            const parsedSeconds = parseInt(retryAfterHeader, 10);
            if (!isNaN(parsedSeconds) && parsedSeconds > 0) {
              cooldownSeconds = parsedSeconds;
            } else {
              const parsedDate = new Date(retryAfterHeader).getTime();
              if (!isNaN(parsedDate) && parsedDate > Date.now()) {
                cooldownSeconds = Math.ceil((parsedDate - Date.now()) / 1000);
              }
            }
          }
          err.retryAfterSeconds = cooldownSeconds;
          rateLimitedUntilMs = Date.now() + cooldownSeconds * 1000;
          // DO NOT clearTokens() on 429! Rate limit is temporary.
          throw err;
        }

        if (status === 401) {
          // 401 indicates invalid or expired refresh token -> end session
          clearTokens();
          if (sessionExpiredCallback) sessionExpiredCallback();
          throw err;
        }

        // 5xx or 403 or other status -> temporary failure, do NOT clearTokens()
        err.isNetworkOrServer = true;
        throw err;
      }

      const data: AuthResponse = await response.json();
      // Successful refresh clears any past rate limit
      rateLimitedUntilMs = 0;

      saveAccessToken(data.accessToken);
      if (data.refreshToken) {
        saveRefreshToken(data.refreshToken);
      }
      if (data.accessTokenExpiresAt) {
        saveAccessTokenExpiresAt(data.accessTokenExpiresAt);
      }
      if (data.deviceTrustToken) {
        saveDeviceTrustToken(data.deviceTrustToken);
      }
      if (data.user) {
        const mappedUser = mapUserProfile(data.user);
        saveStoredUser(mappedUser);
      }
      return data;
    } catch (error: any) {
      // Only clear tokens if genuinely a 401 or no refresh token
      if (error?.status === 401) {
        clearTokens();
        if (sessionExpiredCallback) sessionExpiredCallback();
      }
      throw error;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}


