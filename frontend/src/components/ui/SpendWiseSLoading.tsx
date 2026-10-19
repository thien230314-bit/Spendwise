import React from 'react';
import { cn } from '../../utils/cn';

export type SpendWiseLoadingStatus = 'pending' | 'success' | 'error' | 'offline' | 'timeout' | 'idle';

export interface SpendWiseSLoadingProps {
  /** Visual & functional lifecycle state: pending, success, error, offline, timeout, idle */
  status?: SpendWiseLoadingStatus;
  /** Size in pixels (width & height), defaults to 120 */
  size?: number;
  /** Optional loading status message displayed below */
  label?: string;
  /** Optional sub-label or secondary hint / error description */
  sublabel?: string;
  /** Additional CSS class names for outer wrapper */
  className?: string;
  /** When true, pauses/stops the continuous loop (used in dev preview) */
  paused?: boolean;
}

/**
 * SpendWiseSLoading — Branded Full Lifecycle Animation Component
 *
 * Supported Statuses:
 * - 'pending' : Letter S drawn with animateMotion bead + rotating dual arc outer ring
 * - 'success' : S transitions smoothly to emerald checkmark (✓) with gentle ripple burst
 * - 'error'   : S transitions smoothly to rose error cross (✕) with error halo
 * - 'offline' : Displays disconnected cloud/wifi indicator with slate-amber alert ring
 * - 'timeout' : Displays hourglass / alert icon indicating operation timeout
 * - 'idle'    : Static branded disc placeholder
 */
export const SpendWiseSLoading: React.FC<SpendWiseSLoadingProps> = ({
  status = 'pending',
  size = 120,
  label,
  sublabel,
  className,
  paused = false,
}) => {
  // Unique ID prefix to avoid SVG filter / gradient ID collisions across multiple instances
  const idPrefix = React.useId().replace(/:/g, '');
  const filterGlowId = `${idPrefix}-s-glow`;
  const gradEmeraldId = `${idPrefix}-s-grad`;
  const gradArcId = `${idPrefix}-arc-grad`;
  const gradDiscId = `${idPrefix}-disc-grad`;
  const sPathId = `${idPrefix}-spendwise-s-path`;

  // Exact geometric S path coordinates prescribed in design specifications
  const sPathD =
    'M 122 70 C 116 60 108 56 98 56 C 82 56 72 66 72 78 C 72 92 84 97 100 101 C 116 105 128 110 128 124 C 128 136 116 146 100 146 C 88 146 78 141 72 132';

  // Distinct halo styling based on active lifecycle status
  const haloColorClasses = {
    pending: 'from-emerald-500/25 via-emerald-500/10 to-transparent dark:from-emerald-400/20 dark:via-emerald-500/5',
    success: 'from-emerald-500/35 via-emerald-400/15 to-transparent dark:from-emerald-400/30 dark:via-emerald-500/10',
    error: 'from-rose-500/30 via-rose-500/10 to-transparent dark:from-rose-400/25 dark:via-rose-500/5',
    offline: 'from-amber-500/25 via-slate-500/10 to-transparent dark:from-amber-400/20 dark:via-slate-500/5',
    timeout: 'from-amber-500/30 via-amber-500/10 to-transparent dark:from-amber-400/25 dark:via-amber-500/5',
    idle: 'from-slate-500/15 via-slate-500/5 to-transparent dark:from-slate-400/10',
  };

  const isPending = status === 'pending';
  const isSuccess = status === 'success';
  const isError = status === 'error';
  const isOffline = status === 'offline';
  const isTimeout = status === 'timeout';

  return (
    <div
      className={cn(
        'relative flex flex-col items-center justify-center select-none',
        paused && 'spendwise-s-loading-paused',
        className
      )}
      style={{ width: size, minHeight: size }}
      role="status"
      aria-live="polite"
      aria-label={label || (isSuccess ? 'Thành công' : isError ? 'Thất bại' : isOffline ? 'Mất kết nối mạng' : isTimeout ? 'Hết thời gian chờ' : 'Đang xử lý...')}
    >
      <div
        className="relative flex items-center justify-center"
        style={{ width: size, height: size }}
      >
        {/* A1. Breathing Ambient Background Glow (Radial Blur) */}
        <div
          className={cn(
            'absolute inset-0 rounded-full pointer-events-none -z-10 bg-radial filter blur-xl transition-all duration-500',
            haloColorClasses[status] || haloColorClasses.pending,
            paused || !isPending ? 'opacity-90 scale-100' : 'animate-spendwise-s-breathe'
          )}
          style={{ transformOrigin: 'center center' }}
          aria-hidden="true"
        />

        {/* Master SVG Composition (200 x 200 Coordinate Space) */}
        <svg
          viewBox="0 0 200 200"
          className="w-full h-full overflow-visible drop-shadow-sm transition-all duration-300"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            {/* S Glow Filter */}
            <filter id={filterGlowId} x="-30%" y="-30%" width="160%" height="160%">
              <feGaussianBlur stdDeviation="5" result="glow" />
              <feMerge>
                <feMergeNode in="glow" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>

            {/* Emerald Gradient for S letter & Checkmark: emerald-400 to emerald-600 */}
            <linearGradient id={gradEmeraldId} x1="72" y1="56" x2="128" y2="146" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor="#34D399" />
              <stop offset="50%" stopColor="#10B981" />
              <stop offset="100%" stopColor="#059669" />
            </linearGradient>

            {/* Arc Ring Dual Gradient */}
            <linearGradient id={gradArcId} x1="14" y1="14" x2="186" y2="186" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor={isError ? '#F43F5E' : isOffline || isTimeout ? '#F59E0B' : '#10B981'} stopOpacity="0.9" />
              <stop offset="45%" stopColor={isError ? '#FB7185' : isOffline || isTimeout ? '#FBBF24' : '#34D399'} stopOpacity="0.4" />
              <stop offset="100%" stopColor={isError ? '#E11D48' : isOffline || isTimeout ? '#D97706' : '#059669'} stopOpacity="0.85" />
            </linearGradient>

            {/* Disc Face Radial Gradient */}
            <radialGradient id={gradDiscId} cx="100" cy="100" r="74" gradientUnits="userSpaceOnUse">
              <stop
                offset="0%"
                stopColor={isError ? '#F43F5E' : isOffline || isTimeout ? '#F59E0B' : '#10B981'}
                stopOpacity={isSuccess ? '0.14' : isError ? '0.12' : '0.08'}
              />
              <stop
                offset="70%"
                stopColor={isError ? '#E11D48' : isOffline || isTimeout ? '#D97706' : '#059669'}
                stopOpacity="0.04"
              />
              <stop offset="100%" stopColor="#047857" stopOpacity="0.01" />
            </radialGradient>

            {/* Hidden reusable path for bead animateMotion */}
            <path id={sPathId} d={sPathD} />
          </defs>

          {/* A2. Translucent Inner Circle Face (r="74" at 100,100) */}
          <circle
            cx="100"
            cy="100"
            r="74"
            fill={`url(#${gradDiscId})`}
            className={cn(
              'transition-all duration-300',
              isError
                ? 'stroke-rose-500/20 dark:stroke-rose-400/20'
                : isOffline || isTimeout
                ? 'stroke-amber-500/20 dark:stroke-amber-400/20'
                : 'stroke-emerald-500/15 dark:stroke-emerald-400/15'
            )}
            strokeWidth="1.2"
          />

          {/* A8 / Single Result Ripple Burst upon Success / Error */}
          {isPending && !paused && (
            <circle
              cx="100"
              cy="100"
              r="74"
              className="stroke-emerald-400/60 dark:stroke-emerald-400/80 fill-none pointer-events-none animate-spendwise-s-ripple"
              strokeWidth="2.5"
              style={{ transformOrigin: '100px 100px' }}
            />
          )}

          {isSuccess && (
            <circle
              cx="100"
              cy="100"
              r="74"
              className="stroke-emerald-400/80 dark:stroke-emerald-300/90 fill-none pointer-events-none animate-spendwise-result-ripple"
              strokeWidth="3"
              style={{ transformOrigin: '100px 100px' }}
            />
          )}

          {isError && (
            <circle
              cx="100"
              cy="100"
              r="74"
              className="stroke-rose-400/80 dark:stroke-rose-400/90 fill-none pointer-events-none animate-spendwise-result-ripple"
              strokeWidth="3"
              style={{ transformOrigin: '100px 100px' }}
            />
          )}

          {/* A3. Circular Track (r="86", strokeWidth 6, low opacity static guide) */}
          <circle
            cx="100"
            cy="100"
            r="86"
            className={cn(
              'transition-colors duration-300',
              isError
                ? 'stroke-rose-200/50 dark:stroke-rose-950/50'
                : isOffline || isTimeout
                ? 'stroke-amber-200/50 dark:stroke-amber-950/50'
                : 'stroke-slate-200/70 dark:stroke-slate-800/80'
            )}
            strokeWidth="6"
            strokeLinecap="round"
          />

          {/* Part B. Dual Arc Outer Ring */}
          {/* Active continuous spin only during 'pending'; smoothly stops during result states */}
          <g
            className={cn(
              'pointer-events-none transition-all duration-500',
              isPending && !paused ? 'animate-spendwise-s-dual-arc-spin' : 'opacity-80'
            )}
            style={{ transformOrigin: '100px 100px' }}
          >
            <circle
              cx="100"
              cy="100"
              r="86"
              stroke={`url(#${gradArcId})`}
              strokeWidth="6.5"
              strokeLinecap="round"
              pathLength="100"
              strokeDasharray={isSuccess ? '100 0' : isError ? '15 15' : '25 25'}
              strokeDashoffset={isSuccess ? '0' : '-62.5'}
              className={cn(
                'transition-all duration-500',
                isPending && !paused ? 'animate-spendwise-s-dual-arc-pulse' : ''
              )}
              style={{ transformOrigin: '100px 100px' }}
            />
          </g>

          {/* ================= 1. PENDING STATE: LETTER S ================= */}
          {isPending && (
            <>
              {/* A7. S Glow Duplicate Layer */}
              <path
                d={sPathD}
                pathLength="100"
                stroke="#10B981"
                strokeWidth="13"
                strokeLinecap="round"
                strokeLinejoin="round"
                filter={`url(#${filterGlowId})`}
                className={cn(paused ? 'opacity-30' : 'animate-spendwise-s-path')}
                style={{ opacity: paused ? 0.35 : undefined }}
              />

              {/* A4 & A5. Main Geometric S Path Drawing */}
              <path
                d={sPathD}
                pathLength="100"
                stroke={`url(#${gradEmeraldId})`}
                strokeWidth="11"
                strokeLinecap="round"
                strokeLinejoin="round"
                className={cn(paused ? 'spendwise-s-path-static' : 'animate-spendwise-s-path')}
              />

              {/* A6. Orbiting Light Bead precisely tracking S path */}
              {!paused && (
                <g className="spendwise-s-bead-group pointer-events-none">
                  <circle r="5" fill="#FFFFFF" className="drop-shadow-[0_0_8px_rgba(52,211,153,0.95)]">
                    <animateMotion
                      dur="3.2s"
                      repeatCount="indefinite"
                      keyPoints="0;1;1;0"
                      keyTimes="0;0.6;0.96;1"
                      calcMode="linear"
                    >
                      <mpath href={`#${sPathId}`} />
                    </animateMotion>

                    <animate
                      attributeName="opacity"
                      dur="3.2s"
                      repeatCount="indefinite"
                      values="1;1;0;0;0;1"
                      keyTimes="0;0.58;0.60;0.96;0.98;1"
                    />

                    <animate
                      attributeName="r"
                      dur="1.6s"
                      repeatCount="indefinite"
                      values="4.5;5.5;4.5"
                      calcMode="spline"
                      keySplines="0.45 0 0.55 1; 0.45 0 0.55 1"
                    />
                  </circle>
                </g>
              )}
            </>
          )}

          {/* ================= 2. SUCCESS STATE: S -> CHECKMARK (✓) ================= */}
          {isSuccess && (
            <g className="animate-spendwise-icon-enter" style={{ transformOrigin: '100px 100px' }}>
              {/* Checkmark Background Glow */}
              <path
                d="M 68 102 L 90 124 L 136 78"
                pathLength="40"
                stroke="#10B981"
                strokeWidth="14"
                strokeLinecap="round"
                strokeLinejoin="round"
                filter={`url(#${filterGlowId})`}
                className="opacity-40"
              />
              {/* Animated Stroke Checkmark */}
              <path
                d="M 68 102 L 90 124 L 136 78"
                pathLength="40"
                stroke="#10B981"
                strokeWidth="11"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="animate-spendwise-check"
              />
            </g>
          )}

          {/* ================= 3. ERROR STATE: S -> CROSS (✕) ================= */}
          {isError && (
            <g className="animate-spendwise-icon-enter" style={{ transformOrigin: '100px 100px' }}>
              {/* Cross Line 1 (Top-Left to Bottom-Right) */}
              <path
                d="M 74 74 L 126 126"
                pathLength="35"
                stroke="#F43F5E"
                strokeWidth="10.5"
                strokeLinecap="round"
                className="animate-spendwise-cross-1"
              />
              {/* Cross Line 2 (Top-Right to Bottom-Left) */}
              <path
                d="M 126 74 L 74 126"
                pathLength="35"
                stroke="#F43F5E"
                strokeWidth="10.5"
                strokeLinecap="round"
                className="animate-spendwise-cross-2"
              />
            </g>
          )}

          {/* ================= 4. OFFLINE STATE: DISCONNECTED CLOUD ================= */}
          {isOffline && (
            <g className="animate-spendwise-icon-enter" style={{ transformOrigin: '100px 100px' }}>
              {/* Offline Cloud Body */}
              <path
                d="M 70 120 C 62 120 56 114 56 106 C 56 99 61 93 68 92 C 70 82 78 74 88 74 C 98 74 106 81 109 90 C 113 88 118 89 122 93 C 127 98 128 105 125 111"
                stroke="#F59E0B"
                strokeWidth="7"
                strokeLinecap="round"
                strokeLinejoin="round"
                fill="none"
              />
              {/* Disconnect Slash */}
              <path
                d="M 64 136 L 136 64"
                stroke="#EF4444"
                strokeWidth="6"
                strokeLinecap="round"
              />
            </g>
          )}

          {/* ================= 5. TIMEOUT STATE: HOURGLASS / CLOCK ALERT ================= */}
          {isTimeout && (
            <g className="animate-spendwise-icon-enter" style={{ transformOrigin: '100px 100px' }}>
              {/* Clock Ring */}
              <circle
                cx="100"
                cy="100"
                r="34"
                stroke="#F59E0B"
                strokeWidth="7"
                strokeLinecap="round"
                fill="none"
              />
              {/* Clock Hands: 12 & 3:15 */}
              <path
                d="M 100 80 L 100 100 L 118 100"
                stroke="#F59E0B"
                strokeWidth="6"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              {/* Alert Accent Dot */}
              <circle cx="122" cy="78" r="5" fill="#EF4444" className="animate-ping" />
              <circle cx="122" cy="78" r="5" fill="#EF4444" />
            </g>
          )}

          {/* ================= 6. IDLE STATE: STATIC ACCENT ================= */}
          {status === 'idle' && (
            <circle cx="100" cy="100" r="8" fill="#10B981" className="opacity-80" />
          )}
        </svg>
      </div>

      {/* Localized Status Label */}
      {label && (
        <div className="mt-3.5 text-center px-2 flex flex-col items-center">
          <p
            className={cn(
              'text-xs sm:text-sm font-bold tracking-tight transition-colors',
              isSuccess
                ? 'text-emerald-700 dark:text-emerald-300'
                : isError
                ? 'text-rose-700 dark:text-rose-300'
                : isOffline || isTimeout
                ? 'text-amber-700 dark:text-amber-300'
                : 'text-slate-800 dark:text-slate-100'
            )}
          >
            {label}
          </p>
          {sublabel && (
            <p
              className={cn(
                'text-[11px] mt-0.5 max-w-[260px] leading-snug transition-colors',
                isError
                  ? 'text-rose-600/90 dark:text-rose-400/90 font-medium'
                  : isOffline || isTimeout
                  ? 'text-amber-600/90 dark:text-amber-400/90 font-medium'
                  : 'text-slate-500 dark:text-slate-400'
              )}
            >
              {sublabel}
            </p>
          )}
        </div>
      )}
    </div>
  );
};
