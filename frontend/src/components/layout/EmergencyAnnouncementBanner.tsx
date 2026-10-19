import React, { useState, useEffect } from 'react';
import { AlertTriangle, X, Megaphone } from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import {
  systemOperationsService,
  EmergencyAnnouncement,
} from '../../services/systemOperationsService';

export const EmergencyAnnouncementBanner: React.FC = () => {
  const { currentUser } = useAuth();
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';

  const [activeAnnouncement, setActiveAnnouncement] = useState<EmergencyAnnouncement | null>(() => {
    return systemOperationsService.getActiveAnnouncement();
  });

  const [isDismissed, setIsDismissed] = useState<boolean>(() => {
    const current = systemOperationsService.getActiveAnnouncement();
    if (!current || !currentUser?.id) return false;
    return systemOperationsService.isBannerDismissedForUser(current.id, currentUser.id);
  });

  useEffect(() => {
    const update = () => {
      const current = systemOperationsService.getActiveAnnouncement();
      setActiveAnnouncement(current);
      if (current && currentUser?.id) {
        setIsDismissed(systemOperationsService.isBannerDismissedForUser(current.id, currentUser.id));
      } else {
        setIsDismissed(false);
      }
    };

    update();
    const unsubscribe = systemOperationsService.subscribeAnnouncements(update);
    return () => unsubscribe();
  }, [currentUser?.id]);

  if (!activeAnnouncement || isDismissed) {
    return null;
  }

  const handleDismiss = () => {
    if (currentUser?.id && activeAnnouncement.id) {
      systemOperationsService.dismissBannerForUser(activeAnnouncement.id, currentUser.id);
      setIsDismissed(true);
    }
  };

  const formattedDate = activeAnnouncement.createdAt
    ? new Date(activeAnnouncement.createdAt).toLocaleTimeString(
        locale === 'vi-VN' ? 'vi-VN' : 'en-US',
        { hour: '2-digit', minute: '2-digit' }
      )
    : null;

  return (
    <div
      id="emergency-announcement-banner"
      role="alert"
      className="bg-rose-600 text-white dark:bg-rose-700 border-b border-rose-700 dark:border-rose-800 px-4 py-3 sm:px-6 shadow-sm transition-all animate-in slide-in-from-top-2 duration-200"
    >
      <div className="max-w-[1600px] mx-auto flex items-start sm:items-center justify-between gap-3">
        <div className="flex items-start sm:items-center gap-3 flex-1 min-w-0">
          <div className="p-1.5 rounded-lg bg-white/20 text-white shrink-0 mt-0.5 sm:mt-0">
            <Megaphone className="h-4 w-4" />
          </div>
          <div className="flex-1 min-w-0 text-xs sm:text-sm">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-bold tracking-tight uppercase text-[10px] sm:text-xs bg-white/20 px-2 py-0.5 rounded-sm">
                {isVi ? 'Thông báo khẩn cấp' : 'Emergency Notice'}
              </span>
              <span className="font-bold">{activeAnnouncement.title}</span>
              {formattedDate && (
                <span className="text-[11px] text-rose-100 opacity-90 hidden sm:inline">
                  • {formattedDate}
                </span>
              )}
            </div>
            <p className="text-xs text-rose-100 mt-0.5 leading-relaxed break-words">
              {activeAnnouncement.description}
            </p>
          </div>
        </div>

        {/* Dismiss action */}
        <button
          type="button"
          id="btn-dismiss-emergency-announcement"
          onClick={handleDismiss}
          aria-label={isVi ? 'Đóng thông báo' : 'Dismiss announcement'}
          className="p-1.5 rounded-lg text-rose-100 hover:text-white hover:bg-white/10 transition-colors shrink-0 cursor-pointer"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
};
