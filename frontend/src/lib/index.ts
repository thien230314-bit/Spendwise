/**
 * SpendWise Frontend — API Integration Foundation
 *
 * Public surface of src/lib/ — re-exports the API client, error model,
 * and token storage abstraction for use by feature modules.
 *
 * Modules NOT yet integrated (mocks still active):
 *   auth, wallet, transaction, budget, goal, recurring, friends,
 *   transfer, groups, support, disputes, notifications, AI, banking/SePay
 */

export { apiClient } from './apiClient';
export type { RequestOptions } from './apiClient';

export {
  parseApiError,
  networkError,
  isApiError,
} from './apiError';
export type { ApiError, ApiErrorKind } from './apiError';

export {
  saveAccessToken,
  getAccessToken,
  saveRefreshToken,
  getRefreshToken,
  saveAccessTokenExpiresAt,
  getAccessTokenExpiresAt,
  saveAuthTokens,
  clearTokens,
  hasAccessToken,
} from './tokenStorage';

export { registerSessionExpiredCallback } from './authRefresh';
