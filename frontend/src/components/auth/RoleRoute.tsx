import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../providers/AuthProvider';
import { SystemRole } from '../../types';
import { ShieldAlert, ShieldCheck } from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { ForbiddenPage } from '../../pages/ForbiddenPage';
import { SpendWiseLogo } from '../brand/SpendWiseLogo';

import { isAccessTokenValid, getRefreshToken } from '../../lib/tokenStorage';
import { isRefreshRateLimited, getRefreshCooldownRemainingSeconds } from '../../lib/authRefresh';

interface RoleRouteProps {
  children?: React.ReactNode;
  allowedRoles: SystemRole[];
}

export const RoleRoute: React.FC<RoleRouteProps> = ({ children, allowedRoles }) => {
  const { isAuthenticated, isLoading, currentUser, refreshCurrentUser } = useAuth();
  const location = useLocation();
  const { t } = useI18n();

  // Check whether access token is valid right now (with 0s leeway)
  const [ticker, setTicker] = React.useState(0);
  const hasValidAccessToken = isAccessTokenValid(0);
  const hasRefreshToken = Boolean(getRefreshToken());
  const isRateLimited = isRefreshRateLimited();
  const cooldownRemaining = getRefreshCooldownRemainingSeconds();

  React.useEffect(() => {
    if (!isRateLimited) return;
    const interval = setInterval(() => {
      setTicker(t => t + 1);
    }, 500);
    return () => clearInterval(interval);
  }, [isRateLimited]);

  if (isLoading) {
    return (
      <div className="flex min-h-screen w-full items-center justify-center bg-[#F8F9FA] dark:bg-[#0B1120]">
        <div className="flex flex-col items-center gap-3">
          <div className="relative">
            <SpendWiseLogo size={56} variant="badge" className="animate-pulse shadow-xl shadow-emerald-950/40" />
            <span className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-emerald-600 text-white ring-2 ring-white dark:ring-[#0B1120] shadow-xs">
              <ShieldCheck className="h-3 w-3" />
            </span>
          </div>
          <div className="h-2 w-24 bg-slate-200 dark:bg-slate-800 rounded-full overflow-hidden">
            <div className="h-full bg-emerald-500 rounded-full animate-[shimmer_1.5s_infinite]" />
          </div>
          <span className="text-xs font-medium text-slate-500 dark:text-slate-400">
            {t('common.loading')}
          </span>
        </div>
      </div>
    );
  }

  // Not authenticated at all -> login
  if (!isAuthenticated || !currentUser) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // If authenticated in context (cached credentials present), but access token is expired/invalid:
  // MUST NOT render protected content or allow protected side effects!
  if (!hasValidAccessToken) {
    if (hasRefreshToken) {
      return (
        <div className="flex min-h-screen w-full items-center justify-center bg-[#F8F9FA] dark:bg-[#0B1120] p-4">
          <div className="flex flex-col items-center gap-4 max-w-sm text-center">
            <div className="relative">
              <SpendWiseLogo size={56} variant="badge" className="shadow-xl shadow-emerald-950/40" />
              <span className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-amber-500 text-white ring-2 ring-white dark:ring-[#0B1120] shadow-xs">
                <ShieldAlert className="h-3 w-3" />
              </span>
            </div>
            <div className="space-y-1">
              <h3 className="text-sm font-bold text-slate-800 dark:text-slate-100">
                {isRateLimited ? 'Đang chờ xác thực lại / Reauthenticating' : 'Đang khôi phục phiên / Restoring Session'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {isRateLimited
                  ? `Hệ thống đang tạm giới hạn tần suất yêu cầu. Sẽ thử lại sau ${cooldownRemaining}s.`
                  : 'Vui lòng chờ trong giây lát trong khi hệ thống xác thực lại phiên làm việc.'}
              </p>
            </div>
            {!isRateLimited && (
              <button
                onClick={() => refreshCurrentUser()}
                className="mt-2 inline-flex items-center justify-center rounded-lg bg-emerald-600 px-4 py-2 text-xs font-semibold text-white shadow-xs hover:bg-emerald-500 transition-colors"
              >
                Thử lại ngay
              </button>
            )}
          </div>
        </div>
      );
    }

    // No refresh token either -> redirect login
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (!allowedRoles.includes(currentUser.role)) {
    return <ForbiddenPage />;
  }

  return <>{children}</>;
};
