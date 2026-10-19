import React from 'react';
import { cn } from '../../utils/cn';
import { Wifi, Battery, Signal } from 'lucide-react';

interface MobileDeviceFrameProps {
  children: React.ReactNode;
  theme?: 'light' | 'dark';
  viewport?: 'iphone15' | 'iphone14' | 'pixel';
}

export const MobileDeviceFrame: React.FC<MobileDeviceFrameProps> = ({
  children,
  theme = 'light',
  viewport = 'iphone15',
}) => {
  const isDark = theme === 'dark';

  // Exact smartphone screen aspect ratios:
  // iphone15: 393 x 852
  // iphone14: 375 x 812
  // pixel: 412 x 892
  const dimensions = {
    iphone15: { width: 393, height: 852 },
    iphone14: { width: 375, height: 812 },
    pixel: { width: 412, height: 892 },
  }[viewport];

  return (
    <div className="flex justify-center items-center py-4 select-none">
      {/* Outer Phone Shell */}
      <div
        style={{
          width: dimensions.width + 24,
          height: dimensions.height + 24,
        }}
        className={cn(
          'relative rounded-[54px] p-3 shadow-2xl transition-all duration-300 border-[7px]',
          isDark
            ? 'bg-[#181F2C] border-[#2E3B50] shadow-slate-950/80 ring-1 ring-slate-700/50'
            : 'bg-[#E5E9F0] border-[#D1D7E3] shadow-slate-400/50 ring-1 ring-slate-300/80'
        )}
      >
        {/* Dynamic Island / Hardware Notch */}
        <div className="absolute top-5 left-1/2 -translate-x-1/2 z-50 pointer-events-none">
          <div className="w-28 h-6 bg-black rounded-full flex items-center justify-between px-2.5 shadow-sm">
            <div className="w-2.5 h-2.5 rounded-full bg-[#111] ring-1 ring-white/10" />
            <div className="w-2 h-2 rounded-full bg-emerald-500/80 animate-pulse" />
          </div>
        </div>

        {/* Screen Display Container */}
        <div
          style={{
            width: dimensions.width,
            height: dimensions.height,
          }}
          className={cn(
            'relative overflow-hidden rounded-[42px] flex flex-col font-sans transition-colors duration-200',
            isDark ? 'bg-[#090D16] text-[#F1F5F9]' : 'bg-[#F8FAFC] text-[#0F172A]'
          )}
        >
          {/* Top Status Bar (Safe Area Top) */}
          <div
            className={cn(
              'h-11 px-7 flex items-center justify-between shrink-0 text-xs font-semibold select-none z-40',
              isDark ? 'text-white' : 'text-slate-900'
            )}
          >
            <span className="font-mono text-[11px] tracking-tight pt-0.5">09:41</span>
            <div className="flex items-center gap-2">
              <Signal className="w-3.5 h-3.5" />
              <Wifi className="w-3.5 h-3.5" />
              <Battery className="w-4 h-4" />
            </div>
          </div>

          {/* Screen Content Area */}
          <div className="flex-1 min-h-0 relative flex flex-col overflow-hidden">
            {children}
          </div>

          {/* Bottom Home Indicator Bar (Safe Area Bottom) */}
          <div className="h-6 w-full flex items-center justify-center shrink-0 z-40 pointer-events-none">
            <div
              className={cn(
                'w-32 h-1 rounded-full transition-colors',
                isDark ? 'bg-white/30' : 'bg-black/30'
              )}
            />
          </div>
        </div>
      </div>
    </div>
  );
};
