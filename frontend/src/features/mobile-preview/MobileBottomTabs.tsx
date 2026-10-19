import React, { useEffect, useRef, useState, useId } from 'react';
import { Home, Wallet, ArrowLeftRight, CalendarCheck, Grid2X2 } from 'lucide-react';
import { cn } from '../../utils/cn';
import { soundService } from '../../services/soundService';

export type MobileTab = 'home' | 'wallet' | 'transfer' | 'planning' | 'menu';

interface MobileBottomTabsProps {
  activeTab: MobileTab;
  onTabChange: (tab: MobileTab) => void;
  theme?: 'light' | 'dark';
}

const TABS: { id: MobileTab; label: string; icon: any; freq: number }[] = [
  { id: 'home', label: 'Trang chủ', icon: Home, freq: 523 }, // C5
  { id: 'wallet', label: 'Ví & GD', icon: Wallet, freq: 587 }, // D5
  { id: 'transfer', label: 'Chuyển tiền', icon: ArrowLeftRight, freq: 659 }, // E5
  { id: 'planning', label: 'Kế hoạch', icon: CalendarCheck, freq: 698 }, // F5
  { id: 'menu', label: 'Tiện ích', icon: Grid2X2, freq: 784 }, // G5
];

export const MobileBottomTabs: React.FC<MobileBottomTabsProps> = ({
  activeTab,
  onTabChange,
  theme = 'light',
}) => {
  const isDark = theme === 'dark';
  const filterId = useId();

  const containerRef = useRef<HTMLElement>(null);
  const iconRefs = useRef<{ [key in MobileTab]?: HTMLDivElement | null }>({});

  // Indicator physics state for elastic trailing ring
  // We track head (lead edge) and tail (trailing edge)
  const [indicatorState, setIndicatorState] = useState<{
    leftX: number;
    rightX: number;
    centerY: number;
    radius: number;
    isMoving: boolean;
  } | null>(null);

  // Physics simulation refs
  const physicsRef = useRef<{
    leadX: number;
    tailX: number;
    vLead: number;
    vTail: number;
    targetX: number;
    targetY: number;
    radius: number;
    animating: boolean;
    lastTime: number;
  }>({
    leadX: 0,
    tailX: 0,
    vLead: 0,
    vTail: 0,
    targetX: 0,
    targetY: 0,
    radius: 17, // ~34px diameter ring around 20px icon
    animating: false,
    lastTime: 0,
  });

  const animFrameRef = useRef<number | null>(null);

  // Helper to measure icon position relative to container
  const measureTarget = (tabId: MobileTab) => {
    const iconEl = iconRefs.current[tabId];
    const navEl = containerRef.current;
    if (!iconEl || !navEl) return null;

    const iconRect = iconEl.getBoundingClientRect();
    const navRect = navEl.getBoundingClientRect();

    return {
      centerX: iconRect.left - navRect.left + iconRect.width / 2,
      centerY: iconRect.top - navRect.top + iconRect.height / 2,
    };
  };

  // Check prefers-reduced-motion
  const prefersReducedMotion = typeof window !== 'undefined'
    ? window.matchMedia('(prefers-reduced-motion: reduce)').matches
    : false;

  // Initialize or update position on activeTab change
  useEffect(() => {
    const pos = measureTarget(activeTab);
    if (!pos) return;

    const p = physicsRef.current;
    p.targetX = pos.centerX;
    p.targetY = pos.centerY;

    if (prefersReducedMotion) {
      p.leadX = pos.centerX;
      p.tailX = pos.centerX;
      p.vLead = 0;
      p.vTail = 0;
      setIndicatorState({
        leftX: pos.centerX - p.radius,
        rightX: pos.centerX + p.radius,
        centerY: pos.centerY,
        radius: p.radius,
        isMoving: false,
      });
      return;
    }

    // If first mount or zero positions, snap directly to icon
    if (p.leadX === 0 && p.tailX === 0) {
      p.leadX = pos.centerX;
      p.tailX = pos.centerX;
      p.vLead = 0;
      p.vTail = 0;
      setIndicatorState({
        leftX: pos.centerX - p.radius,
        rightX: pos.centerX + p.radius,
        centerY: pos.centerY,
        radius: p.radius,
        isMoving: false,
      });
      return;
    }

    // Start simulation loop if not already running
    p.lastTime = performance.now();
    p.animating = true;

    const simulate = (time: number) => {
      const dt = Math.min((time - p.lastTime) / 1000, 0.032); // cap frame delta
      p.lastTime = time;

      const target = p.targetX;
      const direction = target >= (p.leadX + p.tailX) / 2 ? 1 : -1;

      // Spring constants:
      // Lead edge is fast and eager with slight overshoot (k ~ 320, d ~ 22)
      // Trail edge is softer during flight and snaps into place when close (k ~ 190, d ~ 19)
      const kLead = 320;
      const dLead = 22;
      const kTail = 210;
      const dTail = 20;

      // When moving right: lead is right edge (max), tail is left edge (min)
      // When moving left: lead is left edge (min), tail is right edge (max)
      let headPos = direction >= 0 ? p.leadX : p.tailX;
      let tailPos = direction >= 0 ? p.tailX : p.leadX;
      let headVel = direction >= 0 ? p.vLead : p.vTail;
      let tailVel = direction >= 0 ? p.vTail : p.vLead;

      // Acceleration = -k*(x - target) - d*v
      const fHead = -kLead * (headPos - target) - dLead * headVel;
      const fTail = -kTail * (tailPos - target) - dTail * tailVel;

      headVel += fHead * dt;
      tailVel += fTail * dt;

      headPos += headVel * dt;
      tailPos += tailVel * dt;

      if (direction >= 0) {
        p.leadX = headPos;
        p.tailX = tailPos;
        p.vLead = headVel;
        p.vTail = tailVel;
      } else {
        p.leadX = tailPos;
        p.tailX = headPos;
        p.vLead = tailVel;
        p.vTail = headVel;
      }

      // Compute left and right bounds of capsule
      const minX = Math.min(p.leadX, p.tailX) - p.radius;
      const maxX = Math.max(p.leadX, p.tailX) + p.radius;
      const distToTarget = Math.abs(p.leadX - target) + Math.abs(p.tailX - target);
      const speed = Math.abs(p.vLead) + Math.abs(p.vTail);

      // Check stabilization condition
      if (distToTarget < 0.8 && speed < 3.5) {
        p.leadX = target;
        p.tailX = target;
        p.vLead = 0;
        p.vTail = 0;
        p.animating = false;
        setIndicatorState({
          leftX: target - p.radius,
          rightX: target + p.radius,
          centerY: p.targetY,
          radius: p.radius,
          isMoving: false,
        });
        return;
      }

      setIndicatorState({
        leftX: minX,
        rightX: maxX,
        centerY: p.targetY,
        radius: p.radius,
        isMoving: true,
      });

      animFrameRef.current = requestAnimationFrame(simulate);
    };

    if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    animFrameRef.current = requestAnimationFrame(simulate);

    return () => {
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    };
  }, [activeTab, prefersReducedMotion]);

  // Recalculate layout on window resize or container resize
  useEffect(() => {
    const handleResize = () => {
      const pos = measureTarget(activeTab);
      if (pos) {
        const p = physicsRef.current;
        p.targetX = pos.centerX;
        p.targetY = pos.centerY;
        p.leadX = pos.centerX;
        p.tailX = pos.centerX;
        p.vLead = 0;
        p.vTail = 0;
        setIndicatorState({
          leftX: pos.centerX - p.radius,
          rightX: pos.centerX + p.radius,
          centerY: pos.centerY,
          radius: p.radius,
          isMoving: false,
        });
      }
    };

    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [activeTab]);

  const handleTabClick = (tab: typeof TABS[0]) => {
    // Rule: Never play sound or re-trigger animation if selecting the currently active tab
    if (tab.id === activeTab) return;

    // Play subtle harmonic tone assigned to tab pitch
    soundService.playTabSound(tab.freq);

    // Call navigation handler
    onTabChange(tab.id);
  };

  // Indicator geometry calculation
  const r = indicatorState ? indicatorState.radius : 17;
  const leftX = indicatorState ? indicatorState.leftX : 0;
  const rightX = indicatorState ? indicatorState.rightX : 0;
  const centerY = indicatorState ? indicatorState.centerY : 20;
  const width = Math.max(rightX - leftX, r * 2);
  const capsuleLeft = leftX;
  const capsuleTop = centerY - r;
  const capsuleHeight = r * 2;

  // Visual Styling tokens matching SpendWise Emerald
  const strokeColor = isDark ? '#34D399' : '#047857';
  const glowInner = isDark ? 'rgba(52, 211, 153, 0.45)' : 'rgba(4, 120, 87, 0.35)';
  const glowOuter = isDark ? 'rgba(52, 211, 153, 0.20)' : 'rgba(4, 120, 87, 0.15)';
  const ambientFill = isDark ? 'rgba(5, 46, 38, 0.35)' : 'rgba(236, 253, 245, 0.55)';

  return (
    <nav
      ref={containerRef}
      aria-label="SpendWise Mobile Navigation"
      className={cn(
        'h-[66px] border-t flex items-center justify-around px-2 shrink-0 z-30 transition-colors relative select-none',
        isDark
          ? 'bg-[#101622] border-[#1E293B]'
          : 'bg-[#FCFBF7] border-[#E2E8F0]'
      )}
    >
      {/* ========================================================
          LIVING ELASTIC GLOW RING & AMBIENT SVG OVERLAY
          ======================================================== */}
      {indicatorState && (
        <svg
          className="absolute inset-0 w-full h-full pointer-events-none z-10 overflow-visible"
          aria-hidden="true"
        >
          <defs>
            <filter id={`nav-ring-glow-${filterId}`} x="-30%" y="-30%" width="160%" height="160%">
              <feGaussianBlur in="SourceGraphic" stdDeviation="2.5" result="blur1" />
              <feGaussianBlur in="SourceGraphic" stdDeviation="5.5" result="blur2" />
              <feMerge>
                <feMergeNode in="blur2" />
                <feMergeNode in="blur1" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>
          </defs>

          {/* Optional subtle bottom reflection (opacity 6%-10%) */}
          <rect
            x={capsuleLeft + 2}
            y={capsuleTop + capsuleHeight + 4}
            width={Math.max(0, width - 4)}
            height={4}
            rx={2}
            fill={glowOuter}
            opacity={isDark ? 0.25 : 0.12}
          />

          {/* Ambient soft backlight behind icon */}
          <rect
            x={capsuleLeft}
            y={capsuleTop}
            width={width}
            height={capsuleHeight}
            rx={r}
            fill={ambientFill}
          />

          {/* Outer diffuse glow */}
          <rect
            x={capsuleLeft}
            y={capsuleTop}
            width={width}
            height={capsuleHeight}
            rx={r}
            fill="none"
            stroke={glowOuter}
            strokeWidth={4.5}
            opacity={0.8}
            filter={`url(#nav-ring-glow-${filterId})`}
          />

          {/* Middle soft glow */}
          <rect
            x={capsuleLeft}
            y={capsuleTop}
            width={width}
            height={capsuleHeight}
            rx={r}
            fill="none"
            stroke={glowInner}
            strokeWidth={2.8}
          />

          {/* Crisp, sharp 1.8px living outline */}
          <rect
            x={capsuleLeft}
            y={capsuleTop}
            width={width}
            height={capsuleHeight}
            rx={r}
            fill="none"
            stroke={strokeColor}
            strokeWidth={1.8}
          />
        </svg>
      )}

      {/* ========================================================
          NAVIGATION TABS (ACCESSIBLE 44x44PX INTERACTION TARGETS)
          ======================================================== */}
      {TABS.map((tab) => {
        const Icon = tab.icon;
        const isActive = activeTab === tab.id;
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={isActive}
            aria-current={isActive ? 'page' : undefined}
            onClick={() => handleTabClick(tab)}
            className={cn(
              'flex-1 min-h-[48px] flex flex-col items-center justify-center py-1 transition-all cursor-pointer relative z-20 outline-none',
              'focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 rounded-xl',
              isActive
                ? isDark
                  ? 'text-[#34D399]'
                  : 'text-[#047857]'
                : isDark
                ? 'text-[#94A3B8] hover:text-[#F1F5F9]'
                : 'text-[#64748B] hover:text-[#0F172A]'
            )}
          >
            {/* Measured Icon Anchor Container */}
            <div
              ref={(el) => {
                iconRefs.current[tab.id] = el;
              }}
              className="w-9 h-9 flex items-center justify-center relative"
            >
              <Icon className={cn('w-5 h-5 transition-transform duration-200 stroke-[2.2]', isActive ? 'scale-105' : 'scale-100')} />
            </div>

            <span
              className={cn(
                'text-[10px] tracking-tight transition-colors duration-150',
                isActive ? 'font-bold' : 'font-medium'
              )}
            >
              {tab.label}
            </span>
          </button>
        );
      })}
    </nav>
  );
};
