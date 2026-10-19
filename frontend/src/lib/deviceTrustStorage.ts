/**
 * SpendWise Frontend — Device Trust Token Storage
 *
 * Deliberately separate from JWT token storage (tokenStorage.ts).
 * Stored in localStorage (persists across browser sessions, unlike access tokens
 * which are stored in sessionStorage).
 */

const DEVICE_TRUST_TOKEN_KEY = 'spendwise_device_trust_token';

let _inMemoryDeviceTrustToken: string | null = null;

export function saveDeviceTrustToken(token: string): void {
  _inMemoryDeviceTrustToken = token;
  try {
    localStorage.setItem(DEVICE_TRUST_TOKEN_KEY, token);
  } catch {}
}

export function getDeviceTrustToken(): string | null {
  if (_inMemoryDeviceTrustToken) return _inMemoryDeviceTrustToken;
  try {
    const stored = localStorage.getItem(DEVICE_TRUST_TOKEN_KEY);
    if (stored) {
      _inMemoryDeviceTrustToken = stored;
      return stored;
    }
  } catch {}
  return null;
}

export function clearDeviceTrustToken(): void {
  _inMemoryDeviceTrustToken = null;
  try {
    localStorage.removeItem(DEVICE_TRUST_TOKEN_KEY);
  } catch {}
}
