import React from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldAlert, ArrowLeft, ShieldCheck, Smartphone, LogOut } from 'lucide-react';
import { useAuth } from '../providers/AuthProvider';
import { useI18n } from '../i18n/I18nContext';

export const ForbiddenPage: React.FC = () => {
  const { currentUser, logout } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();

  const isSystemAdmin = currentUser?.role === 'SystemAdmin';

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <div
      id="forbidden-page"
      className="min-h-screen flex items-center justify-center bg-[var(--bg,#F8F9FA)] dark:bg-[#0B1120] px-4 py-12"
    >
      <div className="max-w-md w-full text-center space-y-6 bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800/80 rounded-2xl p-8 shadow-xl">
        <div className="inline-flex p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/40 text-amber-600 dark:text-amber-400 border border-amber-200 dark:border-amber-900/50">
          <ShieldAlert className="w-12 h-12" />
        </div>

        <div className="space-y-2">
          <span className="inline-block text-xs font-semibold px-2.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700">
            HTTP 403 Forbidden
          </span>
          <h1 className="text-xl font-bold text-slate-900 dark:text-white">
            {t('forbidden.title')}
          </h1>
          <p className="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">
            {isSystemAdmin
              ? 'Tài khoản quản trị viên không đủ quyền để truy cập phân hệ này.'
              : 'Tài khoản người dùng cá nhân chỉ sử dụng trên ứng dụng SpendWise Mobile. Nền tảng Web được thiết kế và bảo vệ nghiêm ngặt chỉ dành riêng cho Quản trị viên hệ thống (SystemAdmin).'}
          </p>
        </div>

        <div className="pt-2 flex flex-col gap-2.5">
          {isSystemAdmin ? (
            <button
              id="btn-goto-admin"
              onClick={() => navigate('/admin')}
              className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 active:scale-[0.99] text-white text-sm font-semibold transition shadow-sm cursor-pointer"
            >
              <ShieldCheck className="w-4 h-4" />
              {t('forbidden.backToAdmin')}
            </button>
          ) : (
            <div className="p-3.5 rounded-xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 text-xs text-emerald-800 dark:text-emerald-300 text-left flex items-start gap-2.5">
              <Smartphone className="w-5 h-5 shrink-0 text-emerald-600 mt-0.5" />
              <span>
                Vui lòng mở ứng dụng <strong>SpendWise Mobile</strong> trên điện thoại để quản lý ví, giao dịch và quỹ nhóm của bạn.
              </span>
            </div>
          )}

          <button
            id="btn-logout"
            onClick={handleLogout}
            className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 text-sm font-medium transition cursor-pointer"
          >
            <LogOut className="w-4 h-4" />
            <span>Đăng xuất / Đăng nhập lại</span>
          </button>
        </div>
      </div>
    </div>
  );
};
