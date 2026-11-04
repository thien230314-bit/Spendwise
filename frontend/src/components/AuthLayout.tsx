import React from 'react';
import { TeddyBearAvatar, TeddyBearHandle } from './TeddyBearAvatar';

interface AuthLayoutProps {
  title: string;
  subtitle?: string;
  isAnimatingS?: boolean;
  bearVariant?: 'login' | 'register';
  bearRef?: React.Ref<TeddyBearHandle>;
  children: React.ReactNode;
}

export const AuthLayout: React.FC<AuthLayoutProps> = ({
  title,
  subtitle,
  isAnimatingS = false,
  bearVariant = 'login',
  bearRef,
  children,
}) => {
  return (
    <div className="relative flex min-h-full flex-col px-4 py-4 sm:py-5">
      <div className="my-auto w-full">
        <div className="text-center">
          <div
            aria-hidden="true"
            data-testid="spendwise-logo-s"
            className={`mx-auto flex h-[38px] w-[38px] items-center justify-center rounded-[12px] bg-gradient-to-br from-[var(--em)] to-[var(--hero1)] text-white shadow-sm border border-white/25 select-none transition-transform duration-200 ${
              isAnimatingS ? 'scale-105' : 'scale-100'
            }`}
          >
            <span className="font-display text-[1.25rem] font-extrabold leading-none tracking-tight text-white">
              S
            </span>
          </div>

          <div className="mt-1">
            <TeddyBearAvatar ref={bearRef} variant={bearVariant} />
          </div>

          <h1 className="mt-2.5 sw-page-title text-balance">
            {title}
          </h1>

          {subtitle ? (
            <p className="mt-1.5 text-[0.9375rem] leading-[1.5] text-[var(--sw-text-secondary)]">
              {subtitle}
            </p>
          ) : null}
        </div>

        {/* High-legibility Auth Panel */}
        <div className="mt-4 sw-panel p-5">
          {children}
        </div>
      </div>
    </div>
  );
};

