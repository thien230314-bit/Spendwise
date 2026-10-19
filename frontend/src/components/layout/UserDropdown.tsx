import React, { useState, useEffect, useRef } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { User as UserIcon, Settings, LogOut, ShieldCheck, Check } from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';

interface UserDropdownProps {
  isOpen: boolean;
  onClose: () => void;
}

export const UserDropdown: React.FC<UserDropdownProps> = ({ isOpen, onClose }) => {
  const { t, locale } = useI18n();
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [loggedOutToast, setLoggedOutToast] = useState(false);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen, onClose]);

  const handleLogout = (e: React.MouseEvent) => {
    e.preventDefault();
    setLoggedOutToast(true);
    setTimeout(() => {
      logout();
      onClose();
      navigate('/login', { replace: true });
    }, 400);
  };

  if (!isOpen) return null;

  const displayName = currentUser?.name || 'SpendWise User';
  const displayEmail = currentUser?.email || 'user@spendwise.vn';
  const displayInitials = currentUser?.avatarInitials || 'U';
  const isAdmin = currentUser?.role === 'SystemAdmin';

  return (
    <div
      ref={dropdownRef}
      className="absolute right-0 top-full mt-2 w-72 rounded-2xl border border-stone-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xl z-50 overflow-hidden animate-in fade-in-0 zoom-in-95 duration-150"
    >
      {/* User Info Header */}
      <div className="p-4 border-b border-stone-100 dark:border-slate-800 bg-stone-50/50 dark:bg-slate-900/50">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-700 text-white font-bold text-sm shadow-sm">
            {displayInitials}
          </div>
          <div className="flex flex-col min-w-0">
            <div className="flex items-center gap-1.5">
              <span className="text-sm font-semibold text-stone-900 dark:text-white truncate">
                {displayName}
              </span>
              {isAdmin && (
                <span className="text-[10px] font-bold px-1.5 py-0.2 rounded bg-amber-100 dark:bg-amber-950 text-amber-800 dark:text-amber-300">
                  ADMIN
                </span>
              )}
            </div>
            <span className="text-xs text-stone-500 dark:text-slate-400 truncate">
              {displayEmail}
            </span>
          </div>
        </div>
        <div className="mt-2.5 flex items-center justify-between text-[11px]">
          <span className="text-stone-500 dark:text-slate-400 font-medium">
            {isAdmin ? (locale === 'vi-VN' ? 'Quản trị viên' : 'System Admin') : (locale === 'vi-VN' ? 'Người dùng' : 'User')}
          </span>
          {currentUser?.phone && (
            <span className="text-stone-400 text-[10px]">
              {currentUser.phone}
            </span>
          )}
        </div>
      </div>

      {/* Menu Options */}
      <div className="p-1.5 space-y-0.5">
        {isAdmin ? (
          <NavLink
            to="/admin/overview"
            onClick={onClose}
            className="flex items-center gap-2.5 px-3 py-2 text-xs font-semibold text-emerald-700 hover:text-emerald-800 hover:bg-emerald-50 dark:text-emerald-400 dark:hover:bg-emerald-950/40 rounded-lg transition-colors"
          >
            <ShieldCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <span>{t('adminNav.consoleTitle')}</span>
          </NavLink>
        ) : (
          <>
            <NavLink
              to="/settings?tab=profile"
              onClick={onClose}
              className="flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-stone-700 hover:text-stone-900 hover:bg-stone-100 dark:text-slate-300 dark:hover:text-white dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <UserIcon className="h-4 w-4 text-stone-400 dark:text-slate-500" />
              <span>{t('common.profile')}</span>
            </NavLink>

            <NavLink
              to="/settings"
              onClick={onClose}
              className="flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-stone-700 hover:text-stone-900 hover:bg-stone-100 dark:text-slate-300 dark:hover:text-white dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <Settings className="h-4 w-4 text-stone-400 dark:text-slate-500" />
              <span>{t('common.settings')}</span>
            </NavLink>
          </>
        )}

        <div className="my-1 border-t border-stone-100 dark:border-slate-800" />

        <button
          type="button"
          onClick={handleLogout}
          className="w-full flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950/40 rounded-lg transition-colors cursor-pointer"
        >
          <LogOut className="h-4 w-4 text-rose-500" />
          <span>{t('common.logout')}</span>
          {loggedOutToast && (
            <span className="ml-auto flex items-center gap-1 text-[11px] text-emerald-600 font-semibold">
              <Check className="h-3 w-3" /> {t('common.loggedOut')}
            </span>
          )}
        </button>
      </div>
    </div>
  );
};

