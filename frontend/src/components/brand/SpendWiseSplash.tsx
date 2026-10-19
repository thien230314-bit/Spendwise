import React, { useEffect, useState, useCallback, useRef } from 'react';
import { SpendWiseLogo } from '../brand/SpendWiseLogo';
import { cn } from '../../utils/cn';

/**
 * Hằng số cấu hình hoạt ảnh Splash 2.1s (dễ tinh chỉnh)
 */
export const SPLASH_CONFIG = {
  // Thời gian (ms)
  totalDurationMs: 2100, // 2.1s: Tổng thời gian hoàn tất
  pressDurationMs: 200, // 0 – 0.2s: Logo lún nhẹ scale 1 -> 0.9
  expandDurationMs: 1000, // 0.2 – 1.2s: Logo phóng to scale 0.9 -> ~1.4, brightness 1 -> 1.2
  sheenStartMs: 350, // 0.35s: Vệt sáng bắt đầu quét
  sheenDurationMs: 900, // 0.35 – 1.25s: Vệt sáng quét qua mặt logo
  glowStartMs: 150, // 0.15s: Vầng sáng cyan & vòng viền mảnh bắt đầu
  glowDurationMs: 1800, // 0.15 – 1.95s: Vầng sáng & vòng viền lan tỏa
  dissolveStartMs: 1200, // 1.2s: Logo bắt đầu mờ nhạt và tan biến
  dissolveDurationMs: 900, // 1.2 – 2.1s: Scale -> ~1.7, brightness -> 1.5, blur 0 -> 6px, opacity -> 0
  reducedMotionDurationMs: 400, // Giảm chuyển động: fade out 0.4s

  // Tỷ lệ kích thước (Scales)
  scaleInitial: 1,
  scalePress: 0.9,
  scaleExpand: 1.4,
  scaleMax: 1.7,
  scaleRingMax: 2.3,

  // Màu sắc & hiệu ứng
  glowColor: '#28E0F0',
  glowOpacityMax: 0.4,
  maxBrightness: 1.5,
  maxBlurPx: 6,
  sheenAngleDeg: 20,
} as const;

export interface SpendWiseSplashProps {
  /** Callback khi animation hoàn tất để chuyển sang màn hình chính */
  onComplete?: () => void;
  /** Khóa reset khi muốn phát lại trong preview */
  replayKey?: string | number;
  /** Tùy chọn class container */
  className?: string;
  /** Tự động ẩn sau khi hoàn tất (default: true) */
  autoDismiss?: boolean;
  /** Tự động bắt đầu animation khi render (default: true) */
  autoPlay?: boolean;
  /** Kích thước cơ sở của logo (default: 120px) */
  logoSize?: number;
}

export const SpendWiseSplash: React.FC<SpendWiseSplashProps> = ({
  onComplete,
  replayKey = 1,
  className,
  autoDismiss = true,
  autoPlay = true,
  logoSize = 120,
}) => {
  const [isPlaying, setIsPlaying] = useState<boolean>(autoPlay);
  const [isFinished, setIsFinished] = useState<boolean>(false);
  const [cycleKey, setCycleKey] = useState<number>(0);
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  // Kiểm tra prefers-reduced-motion của hệ thống
  const checkReducedMotion = () => {
    return (
      typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    );
  };

  const triggerAnimation = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    setIsFinished(false);
    setIsPlaying(true);
    setCycleKey((prev) => prev + 1);

    const isReduced = checkReducedMotion();
    const duration = isReduced
      ? SPLASH_CONFIG.reducedMotionDurationMs
      : SPLASH_CONFIG.totalDurationMs;

    timerRef.current = setTimeout(() => {
      setIsFinished(true);
      setIsPlaying(false);
      if (onComplete) {
        onComplete();
      }
    }, duration);
  }, [onComplete]);

  // Kích hoạt khi mount hoặc replayKey thay đổi
  useEffect(() => {
    if (autoPlay) {
      triggerAnimation();
    } else {
      setIsPlaying(false);
      setIsFinished(false);
    }

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [replayKey, autoPlay, triggerAnimation]);

  // Tính bán kính bo góc 11% chuẩn xác để clip vệt sáng và vòng viền
  const borderRadiusPx = Math.max(2, Math.round(logoSize * 0.11));

  return (
    <div
      role="status"
      aria-live="polite"
      aria-label="SpendWise Startup Splash"
      className={cn(
        'relative w-full h-full min-h-[380px] flex items-center justify-center p-8 select-none transition-colors duration-300 overflow-hidden',
        'bg-[#F8F9FA] dark:bg-[#0B1120]',
        className
      )}
    >
      {/* Khi animation kết thúc hoàn toàn, ẩn toàn bộ logo */}
      {!isFinished && (
        <div
          key={`splash-cycle-${replayKey}-${cycleKey}`}
          onClick={!isPlaying ? triggerAnimation : undefined}
          title={!isPlaying ? 'Bấm để khởi động' : undefined}
          className={cn(
            'relative flex items-center justify-center',
            !isPlaying && 'cursor-pointer hover:scale-105 transition-transform duration-200'
          )}
          style={{ width: `${logoSize}px`, height: `${logoSize}px` }}
        >
          {/* ================= LAYER 0: VẦNG SÁNG CYAN (#28E0F0) (0.15s - 1.95s) ================= */}
          {isPlaying && (
            <div
              className="absolute w-56 h-56 rounded-full pointer-events-none animate-spendwise-splash-glow blur-2xl -z-10"
              style={{
                background: `radial-gradient(circle, rgba(40, 224, 240, ${SPLASH_CONFIG.glowOpacityMax}) 0%, rgba(40, 224, 240, 0.15) 50%, transparent 72%)`,
              }}
              aria-hidden="true"
            />
          )}

          {/* ================= LAYER 1: VÒNG VIỀN MẢNH SCALE 1 -> 2.3 (0.15s - 1.95s) ================= */}
          {isPlaying && (
            <div
              className="absolute inset-0 pointer-events-none animate-spendwise-splash-ring -z-10 border-[1.5px]"
              style={{
                borderRadius: `${borderRadiusPx}px`,
                borderColor: 'rgba(40, 224, 240, 0.85)',
                boxShadow: '0 0 14px rgba(40, 224, 240, 0.55)',
              }}
              aria-hidden="true"
            />
          )}

          {/* ================= LAYER 2: LOGO CHÍNH THỨC VỚI SEQUENCE 2.1S ================= */}
          <div
            className={cn(
              'relative w-full h-full flex items-center justify-center',
              isPlaying ? 'animate-spendwise-splash-core' : 'opacity-100'
            )}
            style={{ borderRadius: `${borderRadiusPx}px` }}
          >
            {/* Logo sắc nét nguyên bản không vẽ lại, dùng asset sẵn có */}
            <SpendWiseLogo
              size={logoSize}
              className="w-full h-full object-contain pointer-events-none select-none"
            />

            {/* ================= LAYER 3: VỆT SÁNG TRẮNG-CYAN NGHIÊNG 20 ĐỘ (0.35s - 1.25s) ================= */}
            {isPlaying && (
              <div
                className="absolute inset-0 overflow-hidden pointer-events-none"
                style={{ borderRadius: `${borderRadiusPx}px` }}
                aria-hidden="true"
              >
                <div
                  className="absolute -top-12 -bottom-12 -left-24 w-28 pointer-events-none animate-spendwise-splash-sheen"
                  style={{
                    background:
                      'linear-gradient(90deg, transparent 0%, rgba(40, 224, 240, 0.25) 25%, rgba(255, 255, 255, 0.95) 50%, rgba(40, 224, 240, 0.3) 75%, transparent 100%)',
                    transform: `rotate(${SPLASH_CONFIG.sheenAngleDeg}deg)`,
                  }}
                />
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};


