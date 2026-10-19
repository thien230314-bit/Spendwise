import { apiClient } from '../lib';
import type { BackendUserProfile } from './authMapper';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: BackendUserProfile;
  /** Present on PIN setup (first-time device trust). May be null on refresh. */
  deviceTrustToken: string | null;
}

export interface ChallengeResponse {
  challengeToken: string;
  status: 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED';
  expiresAt: string;
}

/** Returned by POST /auth/register — backend sends EMAIL_VERIFICATION_REQUIRED first. */
export interface RegisterResponse {
  status: 'EMAIL_VERIFICATION_REQUIRED';
  expiresAt: string;
  resendAfterSeconds: number;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  confirmPassword: string;
}

/** POST /auth/email-verification/verify — returns PIN challenge after OTP confirmed. */
export interface VerifyEmailOtpRequest {
  email: string;
  otp: string;
  deviceName: string;
}

/** POST /auth/email-verification/resend */
export interface ResendEmailOtpRequest {
  email: string;
}

export interface LoginRequest {
  identifier: string;
  password: string;
  deviceName?: string;
}

export interface SetupPinRequest {
  challengeToken: string;
  pin: string;
  confirmPin: string;
}

export interface VerifyPinRequest {
  challengeToken: string;
  pin: string;
  /** Rotate trust on recognised devices. */
  deviceTrustToken?: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
  deviceName?: string;
}

export interface LogoutRequest {
  refreshToken: string;
  deviceName?: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ForgotPasswordRequest {
  identifier: string;
}

export interface ResetPasswordRequest {
  identifier: string;
  otp: string;
  newPassword: string;
}

export interface ForgotPinRequest {
  identifier: string;
}

export interface ValidatePinResetTokenRequest {
  token: string;
}

export interface ValidatePinResetTokenResponse {
  valid: boolean;
  expiresAt: string;
}

export interface ResetPinRequest {
  token: string;
  newPin: string;
  confirmPin: string;
}

export interface SecurityPinStatusResponse {
  isSet: boolean;
}

export interface VerifySecurityPinRequest {
  pin: string;
}

export interface ChangeSecurityPinRequest {
  currentPin: string;
  newPin: string;
  confirmPin: string;
}

export interface UpdatePreferencesRequest {
  preferredLanguage?: string;
  preferredTheme?: string;
  defaultCurrency?: string;
  engagementEmailOptIn?: boolean;
}

export interface AccountRecoveryRequestDto {
  fullName: string;
  phone: string;
  description: string;
  emailHint?: string;
}

export interface AccountRecoveryResponse {
  message: string;
}

export const authApiService = {
  async register(data: RegisterRequest): Promise<RegisterResponse> {
    return apiClient.post('/auth/register', data);
  },

  /** Verify email OTP after registration. Returns a PIN challenge. */
  async verifyEmailOtp(data: VerifyEmailOtpRequest): Promise<ChallengeResponse> {
    return apiClient.post('/auth/email-verification/verify', data);
  },

  /** Resend registration email OTP. Neutral backend response. */
  async resendEmailOtp(data: ResendEmailOtpRequest): Promise<void> {
    return apiClient.post('/auth/email-verification/resend', data);
  },

  async login(data: LoginRequest): Promise<ChallengeResponse> {
    return apiClient.post('/auth/login', data);
  },

  async setupPin(data: SetupPinRequest): Promise<AuthResponse> {
    return apiClient.post('/auth/pin/setup', data);
  },

  async verifyPin(data: VerifyPinRequest): Promise<AuthResponse> {
    return apiClient.post('/auth/pin/verify', data);
  },

  async getCurrentUser(): Promise<BackendUserProfile> {
    return apiClient.get('/users/me');
  },

  async refresh(data: RefreshTokenRequest): Promise<AuthResponse> {
    return apiClient.post('/auth/refresh', data);
  },

  async logout(data: LogoutRequest): Promise<void> {
    return apiClient.post('/auth/logout', data);
  },

  async changePassword(data: ChangePasswordRequest): Promise<void> {
    return apiClient.post('/auth/change-password', data);
  },

  async forgotPassword(data: ForgotPasswordRequest): Promise<void> {
    return apiClient.post('/auth/forgot-password', data);
  },

  /** OTP-based password reset — replaces the old token-link flow. */
  async resetPassword(data: ResetPasswordRequest): Promise<void> {
    return apiClient.post('/auth/reset-password', data);
  },

  async forgotPin(data: ForgotPinRequest): Promise<void> {
    return apiClient.post('/auth/pin/forgot', data);
  },

  async validatePinResetToken(data: ValidatePinResetTokenRequest): Promise<ValidatePinResetTokenResponse> {
    return apiClient.post('/auth/pin/reset/validate', data);
  },

  async resetPin(data: ResetPinRequest): Promise<void> {
    return apiClient.post('/auth/pin/reset', data);
  },

  async getSecurityPinStatus(): Promise<SecurityPinStatusResponse> {
    return apiClient.get('/security-pin/status');
  },

  async verifySecurityPin(data: VerifySecurityPinRequest): Promise<void> {
    return apiClient.post('/security-pin/verify', data);
  },

  async changeSecurityPin(data: ChangeSecurityPinRequest): Promise<void> {
    return apiClient.put('/security-pin', data);
  },

  async revokeAllSessions(): Promise<void> {
    return apiClient.delete('/users/me/sessions');
  },

  async updatePreferences(data: UpdatePreferencesRequest): Promise<BackendUserProfile> {
    return apiClient.put('/users/me/preferences', data);
  },

  async submitAccountRecovery(data: AccountRecoveryRequestDto): Promise<AccountRecoveryResponse> {
    return apiClient.post('/account-recovery', data);
  },
};
