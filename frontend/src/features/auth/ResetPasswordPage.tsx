import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

/**
 * SpendWise — Password Reset Route
 *
 * The active self-service password reset flow is OTP-based via POST /auth/forgot-password
 * and POST /auth/reset-password { identifier, otp, newPassword }.
 * Legacy reset links with ?token= are obsolete; visiting this route automatically redirects
 * the user to the unified Login & Password Recovery workflow.
 */
export const ResetPasswordPage: React.FC = () => {
  const navigate = useNavigate();

  useEffect(() => {
    navigate('/login?action=forgot', { replace: true });
  }, [navigate]);

  return null;
};
