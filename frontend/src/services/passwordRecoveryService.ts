import { authApiService } from './authApiService';
import { isApiError } from '../lib';

export const passwordRecoveryService = {
  /**
   * Step 1 of OTP-based forgot-password flow.
   * Sends POST /auth/forgot-password { identifier }.
   * Backend returns neutral 202 on valid requests to prevent account enumeration.
   * On actual network / API failure, returns failure so caller can notify user without faking success.
   */
  async requestPasswordReset(identifier: string): Promise<{ success: boolean; isRateLimited?: boolean; error?: string }> {
    try {
      await authApiService.forgotPassword({ identifier });
      return { success: true };
    } catch (err) {
      if (isApiError(err) && err.kind === 'rate_limited') {
        return { success: false, isRateLimited: true, error: 'rate_limited' };
      }
      return { success: false, error: 'REQUEST_FAILED' };
    }
  },

  /**
   * Step 2 of OTP-based forgot-password flow.
   * Submits identifier + OTP + newPassword.
   * Returns 204 on success; throws on error.
   */
  async completePasswordReset(
    identifier: string,
    otp: string,
    newPassword: string
  ): Promise<{ success: boolean; error?: string }> {
    try {
      await authApiService.resetPassword({ identifier, otp, newPassword });
      return { success: true };
    } catch {
      return { success: false, error: 'SERVER_ERROR' };
    }
  },

  /**
   * Submits user-initiated account recovery request to locked backend.
   */
  async submitAccountRecoveryRequest(params: {
    fullName: string;
    phone: string;
    description: string;
    emailHint?: string;
  }): Promise<{ success: boolean; error?: string }> {
    try {
      await authApiService.submitAccountRecovery(params);
      return { success: true };
    } catch {
      return { success: false, error: 'SERVER_ERROR' };
    }
  },
};
