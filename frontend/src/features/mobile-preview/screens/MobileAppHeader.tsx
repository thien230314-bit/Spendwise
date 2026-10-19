import React from 'react';
import { cn } from '../../../utils/cn';
import { ArrowLeft } from 'lucide-react';

interface MobileAppHeaderProps {
  title: string;
  subtitle?: string;
  onBack?: () => void;
  rightAction?: React.ReactNode;
  theme?: 'light' | 'dark';
}

export const MobileAppHeader: React.FC<MobileAppHeaderProps> = ({
  title,
  subtitle,
  onBack,
  rightAction,
  theme = 'light',
}) => {
  const isDark = theme === 'dark';

  return (
    <div className="px-5 pt-3 pb-2 flex flex-col gap-0.5 shrink-0">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          {onBack && (
            <button
              type="button"
              onClick={onBack}
              className={cn(
                'p-1.5 -ml-1 rounded-full transition-colors cursor-pointer',
                isDark ? 'hover:bg-slate-800 text-slate-300' : 'hover:bg-slate-100 text-slate-700'
              )}
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
          )}
          <h1
            className={cn(
              'text-xl font-bold tracking-tight',
              isDark ? 'text-[#F1F5F9]' : 'text-[#0F172A]'
            )}
          >
            {title}
          </h1>
        </div>
        {rightAction && <div>{rightAction}</div>}
      </div>

      {subtitle && (
        <p
          className={cn(
            'text-xs leading-relaxed',
            isDark ? 'text-[#94A3B8]' : 'text-[#64748B]'
          )}
        >
          {subtitle}
        </p>
      )}
    </div>
  );
};
