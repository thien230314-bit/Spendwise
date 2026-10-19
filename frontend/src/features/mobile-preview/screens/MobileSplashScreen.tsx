import React, { useEffect, useState } from 'react';
import { cn } from '../../../utils/cn';
import { SpendWiseLogo } from '../../../components/brand/SpendWiseLogo';
import { RotateCcw, Sparkles } from 'lucide-react';

interface MobileSplashScreenProps {
  theme?: 'light' | 'dark';
  onReplay?: () => void;
  onProceed?: () => void;
}

export const MobileSplashScreen: React.FC<MobileSplashScreenProps> = ({
  theme = 'light',
  onReplay,
  onProceed,
}) => {
  const isDark = theme === 'dark';
  const [stage, setStage] = useState<'press' | 'expand' | 'reveal' | 'ready'>('press');

  useEffect(() => {
    // Stage 1: Initial press (scale 0.9)
    const t1 = setTimeout(() => setStage('expand'), 250);
    // Stage 2: Expand & Glow (scale 1.15)
    const t2 = setTimeout(() => setStage('reveal'), 1000);
    // Stage 3: Ready state
    const t3 = setTimeout(() => setStage('ready'), 1800);

    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
      clearTimeout(t3);
    };
  }, []);

  return (
    <div
      className={cn(
        'flex-1 flex flex-col items-center justify-between p-8 relative overflow-hidden transition-colors select-none',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* Background radial ambient glow with pulse animation */}
      <div
        className={cn(
          'absolute w-84 h-84 rounded-full blur-3xl pointer-events-none top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 transition-opacity duration-1000',
          isDark ? 'bg-emerald-500/15' : 'bg-emerald-500/20',
          stage === 'expand' || stage === 'reveal' ? 'opacity-100 scale-125' : 'opacity-40 scale-100'
        )}
      />

      {/* Top Header info */}
      <div className="w-full flex justify-between items-center text-[10px] font-mono tracking-wider opacity-60 z-10">
        <span className="flex items-center gap-1 text-emerald-600 dark:text-emerald-400 font-bold">
          <Sparkles className="w-3 h-3" />
          SPENDWISE MOBILE
        </span>
        <span>VERSION 2.1</span>
      </div>

      {/* Centered Logo & Fintech Animation Container */}
      <div className="flex flex-col items-center justify-center gap-6 z-10 my-auto text-center">
        <div
          className="relative group cursor-pointer"
          onClick={onReplay}
          title="Nhấn để phát lại logo animation"
        >
          {/* Pulsing Cyan & Emerald Rings */}
          <div
            className={cn(
              'absolute -inset-6 rounded-full bg-gradient-to-tr from-emerald-500/40 via-cyan-400/40 to-teal-500/30 blur-2xl transition-all duration-1000',
              stage === 'expand' || stage === 'reveal'
                ? 'scale-130 opacity-90'
                : 'scale-90 opacity-20'
            )}
          />

          {/* Concentric rotating ripple ring */}
          <div
            className={cn(
              'absolute -inset-3 rounded-[32px] border-2 border-emerald-500/40 dark:border-emerald-400/30 transition-all duration-700 pointer-events-none',
              stage === 'reveal' || stage === 'ready' ? 'scale-110 opacity-70 rotate-6' : 'scale-95 opacity-0'
            )}
          />

          {/* Official SpendWise Logo with Scale & Elevation Animation */}
          <div
            className={cn(
              'relative p-3 rounded-3xl bg-white dark:bg-slate-900 shadow-2xl border transition-all duration-700 ease-out',
              isDark ? 'border-slate-800 shadow-emerald-950/60' : 'border-slate-100 shadow-emerald-900/20',
              stage === 'press' && 'scale-90 rotate-0',
              stage === 'expand' && 'scale-110 -rotate-1',
              (stage === 'reveal' || stage === 'ready') && 'scale-100 rotate-0'
            )}
          >
            <SpendWiseLogo size={88} className="transition-transform duration-500" />
          </div>
        </div>

        {/* Wordmark with stagger fade-in */}
        <div
          className={cn(
            'space-y-1 transition-all duration-700 delay-200',
            stage === 'reveal' || stage === 'ready'
              ? 'opacity-100 translate-y-0'
              : 'opacity-0 translate-y-3'
          )}
        >
          <h1 className="text-2xl font-extrabold tracking-tight">
            <span className={isDark ? 'text-white' : 'text-slate-900'}>Spend</span>
            <span className={isDark ? 'text-[#34D399]' : 'text-[#047857]'}>Wise</span>
          </h1>
          <p className="text-[10px] font-bold tracking-widest uppercase text-slate-400">
            NỀN TẢNG TÀI CHÍNH CÁ NHÂN & NHÓM
          </p>
        </div>

        <p
          className={cn(
            'text-xs text-slate-500 dark:text-slate-400 max-w-xs leading-relaxed transition-all duration-700 delay-300',
            stage === 'reveal' || stage === 'ready'
              ? 'opacity-100 translate-y-0'
              : 'opacity-0 translate-y-2'
          )}
        >
          Trải nghiệm ứng dụng di động chính thức SpendWise.
        </p>
      </div>

      {/* Action Buttons */}
      <div
        className={cn(
          'w-full space-y-2.5 z-10 transition-all duration-700 delay-400',
          stage === 'ready' ? 'opacity-100 translate-y-0' : 'opacity-80 translate-y-1'
        )}
      >
        <button
          type="button"
          onClick={onProceed}
          className="w-full py-3.5 px-4 rounded-2xl bg-emerald-600 hover:bg-emerald-700 active:scale-[0.98] text-white text-xs font-bold transition-all shadow-md shadow-emerald-950/20 cursor-pointer flex items-center justify-center gap-1.5"
        >
          <span>Khám phá Trang chủ</span>
          <span>→</span>
        </button>

        {onReplay && (
          <button
            type="button"
            onClick={onReplay}
            className={cn(
              'w-full py-2.5 px-4 rounded-xl border text-xs font-semibold flex items-center justify-center gap-1.5 transition-all cursor-pointer',
              isDark
                ? 'border-slate-800 text-slate-300 hover:bg-slate-800/60'
                : 'border-slate-200 text-slate-700 hover:bg-slate-100'
            )}
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Phát lại Logo Animation</span>
          </button>
        )}
      </div>
    </div>
  );
};
