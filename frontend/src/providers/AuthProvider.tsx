import React, { createContext, useContext, useState, useEffect, ReactNode, useCallback } from 'react';
import { User, SystemRole } from '../types';
import { authApiService, AuthResponse, ChallengeResponse } from '../services/authApiService';
import { mapUserProfile } from '../services/authMapper';
import { getAccessToken, getRefreshToken, saveAccessToken, saveRefreshToken, saveAccessTokenExpiresAt, saveStoredUser, getStoredUser, clearTokens, isAccessTokenValid } from '../lib/tokenStorage';
import { saveDeviceTrustToken, getDeviceTrustToken } from '../lib/deviceTrustStorage';
import { registerSessionExpiredCallback, attemptTokenRefresh, isRefreshRateLimited } from '../lib/authRefresh';
import { isApiError } from '../lib';
import { friendService } from '../services/friendService';

interface PendingChallenge {
  token: string;
  status: 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED';
}

/** Intermediate state between registration form and PIN setup — email OTP verification. */
export interface PendingEmailVerification {
  email: string;
  expiresAt: string;
  resendAfterSeconds: number;
}

interface AuthContextType {
  currentUser: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  role: SystemRole | null;
  isSessionExpired: boolean;
  pendingChallenge: PendingChallenge | null;
  /** Non-null means user just registered and must verify their email. */
  pendingEmailVerification: PendingEmailVerification | null;
  setPendingEmailVerification: (pending: PendingEmailVerification | null) => void;

  initiateLogin: (identifier: string, password: string, deviceName?: string) => Promise<{success: boolean, error?: string, needsPin?: boolean, status?: 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED'}>;
  completePinVerify: (pin: string) => Promise<{success: boolean, user?: User, error?: string}>;
  completePinSetup: (pin: string, confirmPin: string) => Promise<{success: boolean, user?: User, error?: string}>;
  /**
   * Registration step 1: submits form data.
   * On success returns { success: true, needsEmailVerification: true, email, expiresAt, resendAfterSeconds }.
   */
  initiateRegister: (fullName: string, email: string, phone: string, password: string, confirmPassword: string) => Promise<{
    success: boolean;
    needsEmailVerification?: boolean;
    email?: string;
    expiresAt?: string;
    resendAfterSeconds?: number;
    challengeToken?: string;
    status?: 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED';
    error?: string;
  }>;
  /**
   * Registration step 2: verifies email OTP.
   * On success sets pendingChallenge (PIN setup/verify) and returns verified challenge status.
   */
  verifyRegistrationEmail: (otp: string, deviceName?: string) => Promise<{
    success: boolean;
    status?: 'PIN_REQUIRED' | 'PIN_SETUP_REQUIRED';
    error?: string;
  }>;
  /** Resends the registration email OTP. Respects backend 429 rate-limiting. */
  resendRegistrationEmailOtp: () => Promise<{
    success: boolean;
    error?: string;
    isCooldown?: boolean;
  }>;
  completeRegisterPinSetup: (pin: string, confirmPin: string) => Promise<{success: boolean, user?: User, error?: string}>;
  logout: () => Promise<void>;
  refreshCurrentUser: () => Promise<void>;
  triggerSessionExpired: () => void;
  dismissSessionExpired: () => void;
  updateCurrentUser: (updated: Partial<User>) => void;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const sessionSeqRef = React.useRef(0);
  const [currentUser, setCurrentUser] = useState<User | null>(() => {
    const token = getAccessToken();
    const refresh = getRefreshToken();
    if (token || refresh) {
      return getStoredUser<User>();
    }
    return null;
  });
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    const token = getAccessToken();
    const refresh = getRefreshToken();
    const user = getStoredUser<User>();
    return Boolean((token || refresh) && user);
  });
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSessionExpired, setIsSessionExpired] = useState<boolean>(false);
  const [pendingChallenge, setPendingChallenge] = useState<PendingChallenge | null>(null);
  const [pendingEmailVerification, setPendingEmailVerification] = useState<PendingEmailVerification | null>(null);

  /** Persist a received AuthResponse and return the mapped User. */
  const handleAuthResponse = useCallback((authResponse: AuthResponse): User => {
    sessionSeqRef.current += 1;
    saveAccessToken(authResponse.accessToken);
    saveRefreshToken(authResponse.refreshToken);
    if (authResponse.accessTokenExpiresAt) {
      saveAccessTokenExpiresAt(authResponse.accessTokenExpiresAt);
    }
    // Only save a non-null deviceTrustToken — never delete an existing one on null.
    if (authResponse.deviceTrustToken) {
      saveDeviceTrustToken(authResponse.deviceTrustToken);
    }
    const user = mapUserProfile(authResponse.user);
    saveStoredUser(user);
    setCurrentUser(user);
    setIsAuthenticated(true);
    setIsSessionExpired(false);
    setPendingChallenge(null);
    setPendingEmailVerification(null);
    return user;
  }, []);

  /** Apply a challenge response from login/pin-setup without completing auth. */
  const applyChallenge = useCallback((challenge: ChallengeResponse) => {
    setPendingChallenge({ token: challenge.challengeToken, status: challenge.status });
  }, []);

  const refreshCurrentUser = useCallback(async () => {
    const token = getAccessToken();
    const refreshToken = getRefreshToken();
    if (!token && !refreshToken) {
      return;
    }

    const currentSeq = ++sessionSeqRef.current;
    const cachedUser = getStoredUser<User>();

    // If already known to be SystemAdmin:
    // Only refresh if access token is invalid or expired/expiring soon!
    if (cachedUser?.role === 'SystemAdmin') {
      if (token && isAccessTokenValid(60)) {
        // Access token is still valid with at least 60s buffer — NO REFRESH NEEDED!
        if (sessionSeqRef.current === currentSeq) {
          setCurrentUser(cachedUser);
          setIsAuthenticated(true);
          setIsSessionExpired(false);
        }
        return;
      }

      // Access token is missing or expired/expiring soon -> attempt single-flight refresh
      if (refreshToken) {
        // If refresh is currently in a 429 cooldown, do not spam backend
        if (isRefreshRateLimited()) {
          return;
        }

        try {
          const authRes = await attemptTokenRefresh();
          if (authRes && sessionSeqRef.current === currentSeq) {
            handleAuthResponse(authRes);
          }
          return;
        } catch (refreshErr: any) {
          // If refresh was definitively rejected with 401 (invalid/expired refresh token):
          if (refreshErr?.status === 401 && sessionSeqRef.current === currentSeq) {
            clearTokens();
            setCurrentUser(null);
            setIsAuthenticated(false);
            setIsSessionExpired(true);
            return;
          }
          // For temporary errors (429 rate limit, network failure, 5xx):
          // DO NOT clear tokens! Leave credentials intact.
          return;
        }
      }
      return;
    }

    // Try getCurrentUser for regular users (if token is still valid)
    if (token && isAccessTokenValid(60)) {
      try {
        const userProfile = await authApiService.getCurrentUser();
        if (sessionSeqRef.current === currentSeq) {
          const mapped = mapUserProfile(userProfile);
          saveStoredUser(mapped);
          setCurrentUser(mapped);
          setIsAuthenticated(true);
          setIsSessionExpired(false);
        }
        return;
      } catch (err) {
        if (sessionSeqRef.current !== currentSeq) {
          return;
        }

        // If user_role_required (SystemAdmin accessing me endpoint), fallback to cached profile or refresh
        if (isApiError(err) && err.status === 403 && err.code === 'user_role_required') {
          if (token && isAccessTokenValid(60) && cachedUser) {
            setCurrentUser({ ...cachedUser, role: 'SystemAdmin' });
            setIsAuthenticated(true);
            return;
          }
          if (refreshToken && !isRefreshRateLimited()) {
            try {
              const authRes = await attemptTokenRefresh();
              if (authRes && sessionSeqRef.current === currentSeq) {
                handleAuthResponse(authRes);
              }
              return;
            } catch (rErr: any) {
              if (rErr?.status === 401 && sessionSeqRef.current === currentSeq) {
                clearTokens();
                setCurrentUser(null);
                setIsAuthenticated(false);
                setIsSessionExpired(true);
              }
              return;
            }
          }
          return;
        }

        // Only clear tokens if genuinely unauthorized (401)
        if (isApiError(err) && err.kind === 'unauthorized') {
          clearTokens();
          setCurrentUser(null);
          setIsAuthenticated(false);
          setIsSessionExpired(true);
        }
        return;
      }
    }

    // Regular user with expired access token -> try refresh
    if (refreshToken && !isRefreshRateLimited()) {
      try {
        const authRes = await attemptTokenRefresh();
        if (authRes && sessionSeqRef.current === currentSeq) {
          handleAuthResponse(authRes);
        }
      } catch (rErr: any) {
        if (rErr?.status === 401 && sessionSeqRef.current === currentSeq) {
          clearTokens();
          setCurrentUser(null);
          setIsAuthenticated(false);
          setIsSessionExpired(true);
        }
      }
    }
  }, [handleAuthResponse]);

  useEffect(() => {
    registerSessionExpiredCallback(() => {
      sessionSeqRef.current += 1;
      friendService.clearCache();
      clearTokens();
      setIsSessionExpired(true);
      setIsAuthenticated(false);
      setCurrentUser(null);
    });

    let isMounted = true;
    const init = async () => {
      const token = getAccessToken();
      const refreshToken = getRefreshToken();
      if (token || refreshToken) {
        await refreshCurrentUser();
      }
      if (isMounted) {
        setIsLoading(false);
      }
    };

    init();

    return () => {
      isMounted = false;
    };
  }, [refreshCurrentUser]);

  const initiateLogin = useCallback(async (identifier: string, password: string, deviceName?: string) => {
    setIsLoading(true);
    try {
      const res = await authApiService.login({ identifier, password, deviceName });
      setPendingChallenge({ token: res.challengeToken, status: res.status });
      return { success: true, needsPin: true, status: res.status };
    } catch (err) {
      if (isApiError(err)) {
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    } finally {
      setIsLoading(false);
    }
  }, []);

  const completePinVerify = useCallback(async (pin: string) => {
    if (!pendingChallenge) return { success: false, error: 'no_challenge' };
    setIsLoading(true);
    try {
      const deviceTrustToken = getDeviceTrustToken() ?? undefined;
      const res = await authApiService.verifyPin({
        challengeToken: pendingChallenge.token,
        pin,
        deviceTrustToken,
      });
      const user = handleAuthResponse(res);
      return { success: true, user };
    } catch (err) {
      if (isApiError(err)) {
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    } finally {
      setIsLoading(false);
    }
  }, [pendingChallenge, handleAuthResponse]);

  const completePinSetup = useCallback(async (pin: string, confirmPin: string) => {
    if (!pendingChallenge) return { success: false, error: 'no_challenge' };
    setIsLoading(true);
    try {
      const res = await authApiService.setupPin({ challengeToken: pendingChallenge.token, pin, confirmPin });
      const user = handleAuthResponse(res);
      return { success: true, user };
    } catch (err) {
      if (isApiError(err)) {
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    } finally {
      setIsLoading(false);
    }
  }, [pendingChallenge, handleAuthResponse]);

  const initiateRegister = useCallback(async (
    fullName: string,
    email: string,
    phone: string,
    password: string,
    confirmPassword: string
  ) => {
    setIsLoading(true);
    try {
      // POST /auth/register returns { status: 'EMAIL_VERIFICATION_REQUIRED', expiresAt, resendAfterSeconds }
      const res = await authApiService.register({ fullName, email, phone, password, confirmPassword });
      setPendingEmailVerification({
        email,
        expiresAt: res.expiresAt,
        resendAfterSeconds: res.resendAfterSeconds,
      });
      return {
        success: true,
        needsEmailVerification: true,
        email,
        expiresAt: res.expiresAt,
        resendAfterSeconds: res.resendAfterSeconds,
      };
    } catch (err) {
      if (isApiError(err)) {
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    } finally {
      setIsLoading(false);
    }
  }, []);

  const verifyRegistrationEmail = useCallback(async (otp: string, deviceName?: string) => {
    if (!pendingEmailVerification) return { success: false, error: 'no_pending_email' };
    setIsLoading(true);
    try {
      const challenge = await authApiService.verifyEmailOtp({
        email: pendingEmailVerification.email,
        otp,
        deviceName: deviceName || 'browser',
      });
      // After email verify → backend returns a PIN challenge
      applyChallenge(challenge);
      setPendingEmailVerification(null);
      return { success: true, status: challenge.status };
    } catch (err) {
      if (isApiError(err)) {
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    } finally {
      setIsLoading(false);
    }
  }, [pendingEmailVerification, applyChallenge]);

  const resendRegistrationEmailOtp = useCallback(async () => {
    if (!pendingEmailVerification) return { success: false, error: 'no_pending_email' };
    try {
      await authApiService.resendEmailOtp({ email: pendingEmailVerification.email });
      return { success: true };
    } catch (err) {
      if (isApiError(err)) {
        if (err.kind === 'rate_limited') {
          return { success: false, isCooldown: true, error: err.detail || err.title || 'rate_limited' };
        }
        return { success: false, error: err.detail || err.title || err.kind };
      }
      return { success: false, error: 'unknown_error' };
    }
  }, [pendingEmailVerification]);

  const completeRegisterPinSetup = useCallback(async (pin: string, confirmPin: string) => {
    return completePinSetup(pin, confirmPin);
  }, [completePinSetup]);

  const logout = useCallback(async () => {
    sessionSeqRef.current += 1;
    try {
      const refreshToken = getRefreshToken();
      if (refreshToken) {
        await authApiService.logout({ refreshToken });
      }
    } catch (e) {
      // Ignore errors on logout
    } finally {
      friendService.clearCache();
      clearTokens();
      setCurrentUser(null);
      setIsAuthenticated(false);
      setIsSessionExpired(false);
      setPendingChallenge(null);
      setPendingEmailVerification(null);
    }
  }, []);

  const triggerSessionExpired = useCallback(() => {
    sessionSeqRef.current += 1;
    friendService.clearCache();
    clearTokens();
    setCurrentUser(null);
    setIsAuthenticated(false);
    setIsSessionExpired(true);
  }, []);

  const dismissSessionExpired = useCallback(() => {
    setIsSessionExpired(false);
  }, []);

  const updateCurrentUser = useCallback((updated: Partial<User>) => {
    if (currentUser) {
      setCurrentUser({ ...currentUser, ...updated });
    }
  }, [currentUser]);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authApiService.changePassword({ currentPassword, newPassword });
  }, []);

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        isAuthenticated,
        isLoading,
        role: currentUser?.role || null,
        isSessionExpired,
        pendingChallenge,
        pendingEmailVerification,
        setPendingEmailVerification,
        initiateLogin,
        completePinVerify,
        completePinSetup,
        initiateRegister,
        verifyRegistrationEmail,
        resendRegistrationEmailOtp,
        completeRegisterPinSetup,
        logout,
        refreshCurrentUser,
        triggerSessionExpired,
        dismissSessionExpired,
        updateCurrentUser,
        changePassword,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
