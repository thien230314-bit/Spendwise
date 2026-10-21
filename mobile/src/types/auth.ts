export type LoginChallengeStatus = 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED';
export interface RegisterRequest { fullName: string; email: string; phone: string; password: string; confirmPassword: string }
export interface EmailVerificationPendingResponse { status: 'EMAIL_VERIFICATION_REQUIRED'; expiresAt: string; resendAfterSeconds: number }

export interface LoginRequest { identifier: string; password: string; deviceName?: string }
export interface LoginChallengeResponse { challengeToken: string; status: LoginChallengeStatus; expiresAt: string }

export interface UserProfile {
  id: string;
  fullName: string;
  email: string;
  spendId: string;
  phone: string | null;
  systemRole: string;
  status: string;
  mustChangePassword: boolean;
  preferredLanguage: string;
  preferredTheme: string;
  defaultCurrency: string;
  createdAt: string;
  lastLoginAt: string | null;
  engagementEmailOptIn: boolean;
  avatarUrl?: string | null;
  pendingNewEmail?: string | null;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
  deviceTrustToken?: string | null;
}

export interface RefreshRequest { refreshToken: string; deviceName?: string }
