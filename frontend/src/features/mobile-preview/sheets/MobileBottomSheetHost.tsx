import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import { X, Check, Lock, AlertCircle, ShieldCheck } from 'lucide-react';

interface MobileBottomSheetHostProps {
  sheetId: string | null;
  onClose: () => void;
  theme?: 'light' | 'dark';
}

export const MobileBottomSheetHost: React.FC<MobileBottomSheetHostProps> = ({
  sheetId,
  onClose,
  theme = 'light',
}) => {
  if (!sheetId) return null;
  const isDark = theme === 'dark';

  return (
    <div className="absolute inset-0 z-50 flex flex-col justify-end bg-black/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div
        className={cn(
          'w-full max-h-[85%] rounded-t-[36px] border-t p-5 shadow-2xl flex flex-col space-y-4 animate-in slide-in-from-bottom duration-300 overflow-y-auto',
          isDark
            ? 'bg-[#101622] border-[#1E293B] text-white'
            : 'bg-white border-[#E2E8F0] text-slate-900'
        )}
      >
        {/* Handle bar */}
        <div className="w-12 h-1.5 rounded-full bg-slate-300 dark:bg-slate-700 mx-auto -mt-1 mb-1 shrink-0" />

        {/* Header */}
        <div className="flex items-center justify-between pb-2 border-b border-slate-100 dark:border-slate-800">
          <h3 className="text-sm font-bold tracking-tight">
            {sheetId === 'pin-challenge' && 'Xác thực Mã PIN 6 Chữ Số'}
            {sheetId === 'add-transaction' && 'Thêm Giao Dịch Mới'}
            {sheetId === 'wallet-switcher' && 'Chọn Ví Nguồn Trích Tiền'}
            {sheetId === 'add-wallet' && 'Tạo Ví SpendWise Mới'}
            {sheetId === 'link-bank' && 'Liên Kết Ngân Hàng SePay'}
            {sheetId === 'group-fund' && 'Quỹ Nhóm & Chia Tiền'}
            {sheetId === 'notifications' && 'Trung Tâm Thông Báo'}
            {sheetId === 'budget-create' && 'Thiết Lập Ngân Sách Mới'}
            {sheetId === 'goal-create' && 'Khởi Tạo Mục Tiêu Tiết Kiệm'}
            {sheetId === 'recurring-create' && 'Đăng Ký Giao Dịch Định Kỳ'}
          </h3>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded-full text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Content based on sheetId */}
        {sheetId === 'pin-challenge' && (
          <div className="space-y-4 py-2 text-center">
            <div className="w-12 h-12 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto">
              <Lock className="w-6 h-6" />
            </div>
            <div>
              <span className="text-xs font-bold block">Nhập mã PIN để hoàn tất giao dịch</span>
              <span className="text-[11px] text-slate-400">Giao dịch yêu cầu xác thực bảo mật 2 lớp</span>
            </div>

            {/* 6 Digit PIN Boxes */}
            <div className="flex justify-center gap-2.5 py-3">
              {[1, 2, 3, 4, 5, 6].map((i) => (
                <div
                  key={i}
                  className="w-10 h-12 rounded-xl border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 flex items-center justify-center font-bold text-lg"
                >
                  {i <= 4 ? '•' : ''}
                </div>
              ))}
            </div>

            <button
              type="button"
              onClick={onClose}
              className="w-full py-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs cursor-pointer shadow-md"
            >
              Xác nhận chuyển tiền
            </button>
          </div>
        )}

        {sheetId === 'add-transaction' && (
          <div className="space-y-3.5 py-1 text-xs">
            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400">Số tiền (VND)</label>
              <input
                type="text"
                defaultValue="150000"
                className="w-full p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-transparent text-base font-bold font-mono outline-none"
              />
            </div>
            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400">Danh mục</label>
              <select className="w-full p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-transparent outline-none">
                <option value="food">Ăn uống & Cà phê</option>
                <option value="shopping">Mua sắm đồ dùng</option>
                <option value="transport">Đi lại & Xăng xe</option>
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400">Ghi chú giao dịch</label>
              <input
                type="text"
                placeholder="Ví dụ: Ăn trưa với đồng nghiệp"
                className="w-full p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-transparent outline-none"
              />
            </div>
            <button
              type="button"
              onClick={onClose}
              className="w-full py-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer"
            >
              Lưu giao dịch vào ví
            </button>
          </div>
        )}

        {sheetId === 'wallet-switcher' && (
          <div className="space-y-2 py-1 text-xs">
            {[
              { id: '1', name: 'Ví Chi tiêu Cá nhân (Mặc định)', balance: 14250000, isDefault: true },
              { id: '2', name: 'Ví Tiết kiệm Khẩn cấp', balance: 40000000, isDefault: false },
              { id: '3', name: 'Ví Quỹ Du lịch', balance: 8500000, isDefault: false },
            ].map((w) => (
              <div
                key={w.id}
                onClick={onClose}
                className="p-3 rounded-xl border border-slate-200 dark:border-slate-800 hover:border-emerald-500 flex items-center justify-between cursor-pointer transition-colors"
              >
                <div>
                  <span className="font-bold block text-slate-800 dark:text-slate-200">{w.name}</span>
                  <span className="text-[11px] text-slate-400 font-mono">
                    {w.balance.toLocaleString('vi-VN')} ₫
                  </span>
                </div>
                {w.isDefault && (
                  <span className="px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 text-[10px] font-bold">
                    Mặc định
                  </span>
                )}
              </div>
            ))}
          </div>
        )}

        {sheetId === 'link-bank' && (
          <div className="space-y-3.5 py-1 text-xs">
            <div className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-300 text-xs">
              Mã xác thực SWLINK sẽ được tạo để đồng bộ an toàn qua SePay Test Mode.
            </div>
            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400">Chọn ngân hàng</label>
              <select className="w-full p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-transparent outline-none">
                <option value="vcb">Vietcombank (VCB)</option>
                <option value="tcb">Techcombank (TCB)</option>
                <option value="mb">MB Bank (MB)</option>
                <option value="acb">ACB</option>
              </select>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="w-full py-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer"
            >
              Bắt đầu liên kết tài khoản
            </button>
          </div>
        )}

        {sheetId === 'settings' && (
          <div className="space-y-4 py-2 text-xs">
            <div className="p-3.5 rounded-2xl border border-slate-200 dark:border-slate-800 space-y-3">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider block">
                TƯƠNG TÁC & ÂM THANH
              </span>
              <div className="flex items-center justify-between">
                <div>
                  <span className="font-bold text-slate-800 dark:text-slate-200 block text-xs">
                    Âm thanh thao tác (Haptic Audio)
                  </span>
                  <span className="text-[11px] text-slate-400 block mt-0.5">
                    Phát phản hồi âm thanh nhẹ nhàng khi chuyển tab
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    const current = localStorage.getItem('spendwise_haptic_sound_enabled') === 'true';
                    const next = !current;
                    localStorage.setItem('spendwise_haptic_sound_enabled', next ? 'true' : 'false');
                    window.dispatchEvent(new CustomEvent('spendwise_sound_preference_changed', { detail: next }));
                  }}
                  className={cn(
                    "w-12 h-6.5 rounded-full p-0.5 transition-colors cursor-pointer relative",
                    localStorage.getItem('spendwise_haptic_sound_enabled') === 'true'
                      ? "bg-emerald-600"
                      : "bg-slate-300 dark:bg-slate-700"
                  )}
                >
                  <div
                    className={cn(
                      "w-5.5 h-5.5 rounded-full bg-white shadow-xs transition-transform",
                      localStorage.getItem('spendwise_haptic_sound_enabled') === 'true'
                        ? "translate-x-5.5"
                        : "translate-x-0"
                    )}
                  />
                </button>
              </div>
            </div>

            <button
              type="button"
              onClick={onClose}
              className="w-full py-2.5 rounded-xl bg-slate-900 text-white dark:bg-slate-700 font-bold cursor-pointer"
            >
              Lưu & Đóng
            </button>
          </div>
        )}

        {/* Fallback default sheet */}
        {!['pin-challenge', 'add-transaction', 'wallet-switcher', 'link-bank', 'settings'].includes(sheetId) && (
          <div className="space-y-3 py-2 text-center text-xs">
            <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800 text-slate-500">
              Form tương tác Bottom Sheet mô phỏng cho tính năng <strong>{sheetId}</strong>.
            </div>
            <button
              type="button"
              onClick={onClose}
              className="w-full py-2.5 rounded-xl bg-slate-900 text-white dark:bg-slate-700 font-bold cursor-pointer"
            >
              Đóng lại
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
