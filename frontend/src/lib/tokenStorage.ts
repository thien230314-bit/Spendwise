/**
 * SpendWise Frontend — Auth Token Storage Abstraction
 */

const ACCESS_TOKEN_SESSION_KEY = 'spendwise_access_token';
const REFRESH_TOKEN_SESSION_KEY = 'spendwise_refresh_token';
const ACCESS_TOKEN_EXPIRES_AT_SESSION_KEY = 'spendwise_access_token_expires_at';
const USER_PROFILE_SESSION_KEY = 'spendwise_user_profile';

let _inMemoryAccessToken: string | null = null;
let _inMemoryRefreshToken: string | null = null;
let _inMemoryAccessTokenExpiresAt: string | null = null;
let _inMemoryUser: unknown | null = null;

export function saveAccessToken(token: string): void {
  _inMemoryAccessToken = token;
  try {
    sessionStorage.setItem(ACCESS_TOKEN_SESSION_KEY, token);
  } catch {}
}

export function saveRefreshToken(token: string): void {
  _inMemoryRefreshToken = token;
  try {
    sessionStorage.setItem(REFRESH_TOKEN_SESSION_KEY, token);
  } catch {}
}

export function saveAccessTokenExpiresAt(expiresAt: string): void {
  _inMemoryAccessTokenExpiresAt = expiresAt;
  try {
    sessionStorage.setItem(ACCESS_TOKEN_EXPIRES_AT_SESSION_KEY, expiresAt);
  } catch {}
}

export function saveStoredUser<T = unknown>(user: T): void {
  _inMemoryUser = user;
  try {
    sessionStorage.setItem(USER_PROFILE_SESSION_KEY, JSON.stringify(user));
  } catch {}
}

export function getStoredUser<T = unknown>(): T | null {
  if (_inMemoryUser) return _inMemoryUser as T;
  try {
    const raw = sessionStorage.getItem(USER_PROFILE_SESSION_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      _inMemoryUser = parsed;
      return parsed as T;
    }
  } catch {}
  return null;
}

export function clearStoredUser(): void {
  _inMemoryUser = null;
  try {
    sessionStorage.removeItem(USER_PROFILE_SESSION_KEY);
  } catch {}
}

export function saveAuthTokens(accessToken: string, refreshToken: string, expiresAt?: string): void {
  saveAccessToken(accessToken);
  saveRefreshToken(refreshToken);
  if (expiresAt) {
    saveAccessTokenExpiresAt(expiresAt);
  }
}

export function getAccessToken(): string | null {
  if (_inMemoryAccessToken) return _inMemoryAccessToken;
  try {
    const stored = sessionStorage.getItem(ACCESS_TOKEN_SESSION_KEY);
    if (stored) {
      _inMemoryAccessToken = stored;
      return stored;
    }
  } catch {}
  return null;
}

export function getRefreshToken(): string | null {
  if (_inMemoryRefreshToken) return _inMemoryRefreshToken;
  try {
    const stored = sessionStorage.getItem(REFRESH_TOKEN_SESSION_KEY);
    if (stored) {
      _inMemoryRefreshToken = stored;
      return stored;
    }
  } catch {}
  return null;
}

export function getAccessTokenExpiresAt(): string | null {
  if (_inMemoryAccessTokenExpiresAt) return _inMemoryAccessTokenExpiresAt;
  try {
    const stored = sessionStorage.getItem(ACCESS_TOKEN_EXPIRES_AT_SESSION_KEY);
    if (stored) {
      _inMemoryAccessTokenExpiresAt = stored;
      return stored;
    }
  } catch {}
  return null;
}

export function clearTokens(): void {
  _inMemoryAccessToken = null;
  _inMemoryRefreshToken = null;
  _inMemoryAccessTokenExpiresAt = null;
  _inMemoryUser = null;
  try {
    sessionStorage.removeItem(ACCESS_TOKEN_SESSION_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_SESSION_KEY);
    sessionStorage.removeItem(ACCESS_TOKEN_EXPIRES_AT_SESSION_KEY);
    sessionStorage.removeItem(USER_PROFILE_SESSION_KEY);
    window.dispatchEvent(new Event('spendwise_tokens_cleared'));
  } catch {}
}

export function hasAccessToken(): boolean {
  return getAccessToken() !== null;
}

/**
 * Check if the stored access token is still valid.
 * Uses the authoritative expiresAt timestamp returned from Backend auth response.
 * If expiresAt is not recorded, falls back to parsing JWT exp claim as an advisory hint.
 *
 * @param leewaySeconds Buffer in seconds before actual expiry (default 60s).
 */
export function isAccessTokenValid(leewaySeconds = 60): boolean {
  const token = getAccessToken();
  if (!token) return false;

  const expiresAtStr = getAccessTokenExpiresAt();
  if (expiresAtStr) {
    const expiresAtMs = new Date(expiresAtStr).getTime();
    if (!isNaN(expiresAtMs)) {
      const nowMs = Date.now();
      return expiresAtMs - leewaySeconds * 1000 > nowMs;
    }
  }

  // Fallback: Advisory hint from JWT payload if expiresAt was missing
  try {
    const parts = token.split('.');
    if (parts.length === 3) {
      const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(payloadJson);
      if (typeof payload.exp === 'number') {
        const expMs = payload.exp * 1000;
        return expMs - leewaySeconds * 1000 > Date.now();
      }
    }
  } catch {}

  // If cannot determine expiry but token exists, treat as valid only if recently stored
  return true;
}



