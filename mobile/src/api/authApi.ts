import { apiRequest } from './client';
import type { AuthResponse, EmailVerificationPendingResponse, RegisterRequest, LoginChallengeResponse, LoginRequest, RefreshRequest } from '@/types/auth';

export const authApi = {
  register: (request: RegisterRequest) => apiRequest<EmailVerificationPendingResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify(request), retryAfterRefresh: false }),
  verifyRegistrationEmail: (email: string, otp: string) => apiRequest<LoginChallengeResponse>('/api/auth/email-verification/verify', { method: 'POST', body: JSON.stringify({ email, otp, deviceName: 'SpendWise Mobile' }), retryAfterRefresh: false }),
  resendRegistrationEmail: (email: string) => apiRequest<EmailVerificationPendingResponse>('/api/auth/email-verification/resend', { method: 'POST', body: JSON.stringify({ email }), retryAfterRefresh: false }),
  forgotPassword: (identifier: string) => apiRequest<{ message: string }>('/api/auth/forgot-password', { method: 'POST', body: JSON.stringify({ identifier }), retryAfterRefresh: false }),
  resetPassword: (identifier: string, otp: string, newPassword: string) => apiRequest<void>('/api/auth/reset-password', { method: 'POST', body: JSON.stringify({ identifier, otp, newPassword }), retryAfterRefresh: false }),
  forgotPin: (identifier: string) => apiRequest<{ message: string }>('/api/auth/pin/forgot', { method: 'POST', body: JSON.stringify({ identifier }), retryAfterRefresh: false }),
  validatePinReset: (token: string) => apiRequest<{ valid: boolean; expiresAt: string }>('/api/auth/pin/reset/validate', { method: 'POST', body: JSON.stringify({ token }), retryAfterRefresh: false }),
  resetPin: (token: string, newPin: string, confirmPin: string) => apiRequest<void>('/api/auth/pin/reset', { method: 'POST', body: JSON.stringify({ token, newPin, confirmPin }), retryAfterRefresh: false }),
  requestAccountRecovery: (request: { fullName: string; phone: string; description: string; emailHint?: string }) => apiRequest<{ message: string }>('/api/account-recovery', { method: 'POST', body: JSON.stringify(request), retryAfterRefresh: false }),
  login: (request: LoginRequest) => apiRequest<LoginChallengeResponse>('/api/auth/login', {
    method: 'POST', body: JSON.stringify(request), retryAfterRefresh: false,
  }),
  verifyPin: (challengeToken: string, pin: string, deviceTrustToken?: string | null) =>
    apiRequest<AuthResponse>('/api/auth/pin/verify', {
      method: 'POST', body: JSON.stringify({ challengeToken, pin, deviceTrustToken: deviceTrustToken ?? null }), retryAfterRefresh: false,
    }),
  setupPin: (challengeToken: string, pin: string, confirmPin: string) =>
    apiRequest<AuthResponse>('/api/auth/pin/setup', {
      method: 'POST', body: JSON.stringify({ challengeToken, pin, confirmPin }), retryAfterRefresh: false,
    }),
  refresh: (request: RefreshRequest) => apiRequest<AuthResponse>('/api/auth/refresh', {
    method: 'POST', body: JSON.stringify(request), retryAfterRefresh: false,
  }),
  logout: (refreshToken: string) => apiRequest<void>('/api/auth/logout', {
    method: 'POST', authenticated: true, retryAfterRefresh: false,
    body: JSON.stringify({ refreshToken, deviceName: 'SpendWise Mobile' }),
  }),
};
