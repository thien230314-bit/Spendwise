import { authApiService } from './authApiService';
import { isApiError } from '../lib';

export type PinResetTokenStatus =
  | 'validating'
  | 'valid'
  | 'invalid'
  | 'expired'
  | 'already-used'
  | 'network-error'
  | 'reset-success';

export function isValidSecurityPin(pin: string): boolean {
  return /^\d{6}$/.test(pin);
}

export const securityPinService = {
  /**
   * Asynchronously retrieves Security PIN status from the backend.
   * GET /security-pin/status
   */
  async getStatus(): Promise<{ isSet: boolean }> {
    const res = await authApiService.getSecurityPinStatus();
    return { isSet: Boolean(res.isSet) };
  },

  /**
   * Check if user has an active Security PIN enrolled.
   */
  async hasPin(_userId?: string): Promise<boolean> {
    const status = await this.getStatus();
    return status.isSet;
  },

  /**
   * Verify authenticated user's 6-digit Security PIN against the backend.
   * POST /security-pin/verify
   */
  async verifyPin(userIdOrPin: string, pin?: string): Promise<boolean> {
    const candidatePin = pin !== undefined ? pin : userIdOrPin;
    if (!isValidSecurityPin(candidatePin)) {
      return false;
    }
    try {
      await authApiService.verifySecurityPin({ pin: candidatePin });
      return true;
    } catch (err: unknown) {
      if (isApiError(err) && (err.code === 'pin_verification_failed' || (err.status === 401 && err.code !== 'unauthorized'))) {
        return false;
      }
      throw err;
    }
  },

  /**
   * Change authenticated user's Security PIN.
   * PUT /security-pin
   */
  async changePin(currentPin: string, newPin: string, confirmPin: string): Promise<{ success: boolean; error?: string }> {
    if (!isValidSecurityPin(newPin) || newPin !== confirmPin) {
      return { success: false, error: 'INVALID_PIN' };
    }
    try {
      await authApiService.changeSecurityPin({ currentPin, newPin, confirmPin });
      return { success: true };
    } catch (err: any) {
      return { success: false, error: err?.detail || err?.title || 'FAILED_TO_CHANGE' };
    }
  },

  /**
   * Create self-service PIN reset request (neutral 202 response).
   * POST /auth/pin/forgot
   */
  async createPinResetRequest(identifier: string): Promise<{
    success: boolean;
    error?: string;
  }> {
    try {
      await authApiService.forgotPin({ identifier });
      return { success: true };
    } catch (err: any) {
      // If 404/not found or user not found, return neutral success to prevent enumeration
      if (err?.status === 404 || err?.status === 202 || err?.status === 200) {
        return { success: true };
      }
      // Re-throw genuine network or 5xx/400 errors so caller knows it failed
      throw err;
    }
  },

  /**
   * Validate a PIN reset token with backend.
   * POST /auth/pin/reset/validate
   */
  async validatePinResetToken(token: string): Promise<{
    status: PinResetTokenStatus;
    expiresAt?: string;
  }> {
    if (!token) {
      return { status: 'invalid' };
    }
    try {
      const res = await authApiService.validatePinResetToken({ token });
      if (res && res.valid) {
        return { status: 'valid', expiresAt: res.expiresAt };
      }
      return { status: 'invalid' };
    } catch (err: any) {
      if (err?.code === 'token_expired' || err?.detail?.includes?.('expired')) {
        return { status: 'expired' };
      }
      if (err?.code === 'token_used' || err?.detail?.includes?.('consumed') || err?.detail?.includes?.('used')) {
        return { status: 'already-used' };
      }
      if (err?.status === 400 || err?.status === 401 || err?.status === 404) {
        return { status: 'invalid' };
      }
      // Network failure or 5xx server error
      return { status: 'network-error' };
    }
  },

  /**
   * Complete PIN reset using valid token and new PIN.
   * POST /auth/pin/reset
   */
  async completePinReset(token: string, newPin: string, confirmPin?: string): Promise<{
    success: boolean;
    error?: string;
  }> {
    if (!isValidSecurityPin(newPin)) {
      return { success: false, error: 'INVALID_PIN' };
    }
    try {
      await authApiService.resetPin({
        token,
        newPin,
        confirmPin: confirmPin || newPin,
      });
      return { success: true };
    } catch (err: any) {
      return { success: false, error: err?.detail || err?.title || 'SERVER_ERROR' };
    }
  },
};

