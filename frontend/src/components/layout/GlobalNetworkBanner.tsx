import React, { useEffect, useState } from 'react';
import { WifiOff, CheckCircle2, X } from 'lucide-react';
import { networkStatusService } from '../../services/networkStatusService';
import { useI18n } from '../../i18n/I18nContext';
import { cn } from '../../utils/cn';

export interface GlobalNetworkBannerProps {
  /** Optional override for standalone dev preview */
  mockState?: 'online' | 'offline' | 'reconnected' | null;
  className?: string;
}

export const GlobalNetworkBanner: React.FC<GlobalNetworkBannerProps> = ({
  mockState = null,
  className,
}) => {
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';

  const [isOnline, setIsOnline] = useState<boolean>(() => networkStatusService.getStatus().isOnline);
  const [showReconnected, setShowReconnected] = useState<boolean>(false);
  const [isDismissed, setIsDismissed] = useState<boolean>(false);

  useEffect(() => {
    if (mockState !== null) {
      if (mockState === 'offline') {
        setIsOnline(false);
        setShowReconnected(false);
        setIsDismissed(false);
      } else if (mockState === 'reconnected') {
        setIsOnline(true);
        setShowReconnected(true);
        setIsDismissed(false);
      } else {
        setIsOnline(true);
        setShowReconnected(false);
        setIsDismissed(false);
      }
      return;
    }

    const unsubscribe = networkStatusService.subscribe((online, wasOffline) => {
      setIsOnline(online);
      setIsDismissed(false);

      if (online && wasOffline) {
        setShowReconnected(true);
        const timer = setTimeout(() => {
          setShowReconnected(false);
        }, 4000);
        return () => clearTimeout(timer);
      } else if (!online) {
        setShowReconnected(false);
      }
    });

    return () => unsubscribe();
  }, [mockState]);

  // If online and not showing temporary reconnected message, render nothing
  if ((isOnline && !showReconnected) || isDismissed) {
    return null;
  }

  return (
    <aside
      aria-label={isOnline ? (isVi ? 'Đã kết nối lại Internet' : 'Internet connection restored') : (isVi ? 'Mất kết nối Internet' : 'No Internet Connection')}
      aria-live="polite"
      className={cn(
        'w-full px-4 py-2.5 transition-all duration-300 z-50 select-none shadow-xs border-b',
        isOnline
          ? 'bg-emerald-600 text-white border-emerald-700'
          : 'bg-amber-600 dark:bg-amber-700 text-white border-amber-800',
        className
      )}
    >
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-3 text-xs sm:text-sm">
        <div className="flex items-center gap-2.5 min-w-0">
          {isOnline ? (
            <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-100 animate-in zoom-in-75 duration-200" />
          ) : (
            <WifiOff className="h-4 w-4 shrink-0 text-amber-100 animate-pulse" />
          )}

          <div className="min-w-0">
            <span className="font-bold mr-2">
              {isOnline
                ? isVi
                  ? 'Đã kết nối lại Internet'
                  : 'Internet Reconnected'
                : isVi
                ? 'Không có kết nối Internet'
                : 'No Internet Connection'}
            </span>
            <span className="opacity-90 text-[11px] sm:text-xs">
              {isOnline
                ? isVi
                  ? 'Ứng dụng đã sẵn sàng đồng bộ dữ liệu.'
                  : 'Your connection has been restored.'
                : isVi
                ? 'Một số tính năng và dữ liệu mới có thể chưa được cập nhật.'
                : 'Live updates and new transactions may be temporarily delayed.'}
            </span>
          </div>
        </div>

        {/* Dismiss Button */}
        <button
          type="button"
          onClick={() => setIsDismissed(true)}
          className="p-1 rounded-lg hover:bg-black/15 active:bg-black/25 text-white transition-colors cursor-pointer shrink-0"
          title={isVi ? 'Đóng thông báo' : 'Dismiss'}
        >
          <X className="h-3.5 w-3.5" />
        </button>
      </div>
    </aside>
  );
};
