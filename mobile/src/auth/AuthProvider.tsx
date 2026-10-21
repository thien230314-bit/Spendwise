import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { authApi } from '@/api/authApi';
import { configureApiAuth } from '@/api/client';
import { tokenStorage } from './tokenStorage';
import type { AuthResponse, LoginChallengeResponse, UserProfile } from '@/types/auth';

export type AuthStatus = 'initializing' | 'unauthenticated' | 'challenge-pending' | 'authenticated';
type AuthContextValue = {
  status: AuthStatus; user: UserProfile | null; challenge: LoginChallengeResponse | null;
  login: (identifier: string, password: string) => Promise<LoginChallengeResponse>;
  verifyRegistrationEmail: (email: string, otp: string) => Promise<LoginChallengeResponse>;
  completePin: (pin: string, confirmPin?: string) => Promise<void>;
  cancelChallenge: () => void; logout: () => Promise<void>; updateUser: (updatedUser: UserProfile) => void;
};
const AuthContext = createContext<AuthContextValue | null>(null);
const USER_ONLY_MESSAGE = 'Ứng dụng di động SpendWise hiện chỉ hỗ trợ tài khoản người dùng.';

export function AuthProvider({ children }: PropsWithChildren) {
  const [status, setStatus] = useState<AuthStatus>('initializing');
  const [user, setUser] = useState<UserProfile | null>(null);
  const [challenge, setChallenge] = useState<LoginChallengeResponse | null>(null);
  const accessTokenRef = useRef<string | null>(null);
  const refreshTokenRef = useRef<string | null>(null);
  const deviceTrustTokenRef = useRef<string | null>(null);

  const clearLocalSession = useCallback(async () => {
    accessTokenRef.current = null; refreshTokenRef.current = null; deviceTrustTokenRef.current = null;
    setUser(null); setChallenge(null); await tokenStorage.clear();
  }, []);

  const acceptAuth = useCallback(async (auth: AuthResponse) => {
    accessTokenRef.current = auth.accessToken; refreshTokenRef.current = auth.refreshToken;
    deviceTrustTokenRef.current = auth.deviceTrustToken ?? deviceTrustTokenRef.current;
    await tokenStorage.save({ accessToken: auth.accessToken, refreshToken: auth.refreshToken, deviceTrustToken: deviceTrustTokenRef.current });
    if (auth.user.systemRole === 'SystemAdmin') {
      try { await authApi.logout(auth.refreshToken); } finally { await clearLocalSession(); }
      setStatus('unauthenticated');
      throw new Error(USER_ONLY_MESSAGE);
    }
    setUser(auth.user); setChallenge(null); setStatus('authenticated');
    return auth.accessToken;
  }, [clearLocalSession]);

  const refreshSession = useCallback(async () => {
    const refreshToken = refreshTokenRef.current;
    if (!refreshToken) return null;
    try {
      return await acceptAuth(await authApi.refresh({ refreshToken, deviceName: 'SpendWise Mobile' }));
    } catch {
      await clearLocalSession(); setStatus('unauthenticated'); return null;
    }
  }, [acceptAuth, clearLocalSession]);

  useEffect(() => {
    configureApiAuth({ getAccessToken: () => accessTokenRef.current, refreshSession });
  }, [refreshSession]);

  useEffect(() => {
    let active = true;
    (async () => {
      const stored = await tokenStorage.read();
      if (!active) return;
      accessTokenRef.current = stored.accessToken; refreshTokenRef.current = stored.refreshToken;
      deviceTrustTokenRef.current = stored.deviceTrustToken;
      if (!stored.refreshToken) { setStatus('unauthenticated'); return; }
      await refreshSession();
    })().catch(async () => { await clearLocalSession(); if (active) setStatus('unauthenticated'); });
    return () => { active = false; };
  }, [clearLocalSession, refreshSession]);

  const login = useCallback(async (identifier: string, password: string) => {
    const result = await authApi.login({ identifier, password, deviceName: 'SpendWise Mobile' });
    setChallenge(result); setStatus('challenge-pending'); return result;
  }, []);

  const verifyRegistrationEmail = useCallback(async (email: string, otp: string) => {
    const result = await authApi.verifyRegistrationEmail(email, otp);
    setChallenge(result); setStatus('challenge-pending'); return result;
  }, []);

  const completePin = useCallback(async (pin: string, confirmPin?: string) => {
    if (!challenge) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
    const auth = challenge.status === 'PIN_SETUP_REQUIRED'
      ? await authApi.setupPin(challenge.challengeToken, pin, confirmPin ?? '')
      : await authApi.verifyPin(challenge.challengeToken, pin, deviceTrustTokenRef.current);
    await acceptAuth(auth);
  }, [acceptAuth, challenge]);

  const cancelChallenge = useCallback(() => { setChallenge(null); setStatus('unauthenticated'); }, []);
  const updateUser = useCallback((updatedUser: UserProfile) => { setUser(updatedUser); }, []);

  const logout = useCallback(async () => {
    const refreshToken = refreshTokenRef.current;
    try { if (refreshToken && accessTokenRef.current) await authApi.logout(refreshToken); }
    finally { await clearLocalSession(); setStatus('unauthenticated'); }
  }, [clearLocalSession]);

  const value = useMemo(() => ({ status, user, challenge, login, verifyRegistrationEmail, completePin, cancelChallenge, logout, updateUser }),
    [status, user, challenge, login, verifyRegistrationEmail, completePin, cancelChallenge, logout, updateUser]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider.');
  return value;
}
