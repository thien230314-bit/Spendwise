import React, { useRef, useEffect } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { cn } from '../../utils/cn';
import { useI18n } from '../../i18n/I18nContext';

export interface SixDigitPinInputProps {
  value: string;
  onChange: (value: string) => void;
  onComplete?: (pin: string) => void;
  disabled?: boolean;
  error?: boolean;
  autoFocus?: boolean;
  mask?: boolean;
  showToggle?: boolean;
  onToggleMask?: () => void;
  id?: string;
  ariaLabel?: string;
}

export const SixDigitPinInput: React.FC<SixDigitPinInputProps> = ({
  value,
  onChange,
  onComplete,
  disabled = false,
  error = false,
  autoFocus = true,
  mask = true,
  showToggle = false,
  onToggleMask,
  id = 'six-digit-pin-input',
  ariaLabel = 'Security PIN',
}) => {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const submittedPinRef = useRef<string | null>(null);

  useEffect(() => {
    if (autoFocus && !disabled) {
      const timer = setTimeout(() => {
        if (inputRef.current) {
          inputRef.current.focus();
          const len = inputRef.current.value.length;
          inputRef.current.setSelectionRange(len, len);
        }
      }, 50);
      return () => clearTimeout(timer);
    }
  }, [autoFocus, disabled]);

  // Reset single-flight submittedPinRef whenever value clears or changes to < 6
  useEffect(() => {
    if (value.length < 6) {
      submittedPinRef.current = null;
    }
  }, [value]);

  const keepCaretAtEnd = () => {
    if (inputRef.current) {
      const len = inputRef.current.value.length;
      inputRef.current.setSelectionRange(len, len);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const rawVal = e.target.value;
    const clean = rawVal.replace(/\D/g, '').slice(0, 6);
    onChange(clean);
    if (clean.length === 6 && onComplete) {
      if (submittedPinRef.current !== clean) {
        submittedPinRef.current = clean;
        onComplete(clean);
      }
    }
  };

  const handleContainerClick = () => {
    if (!disabled && inputRef.current) {
      inputRef.current.focus();
      keepCaretAtEnd();
    }
  };

  const digits = value.split('');

  return (
    <div className="flex flex-col items-center gap-2">
      <div
        onClick={handleContainerClick}
        className="relative flex items-center justify-center gap-2.5 cursor-pointer select-none"
      >
        {/* Real hidden numeric input capturing keystrokes */}
        <input
          ref={inputRef}
          id={id}
          type={mask ? 'password' : 'text'}
          inputMode="numeric"
          pattern="[0-9]*"
          maxLength={6}
          autoComplete="one-time-code"
          value={value}
          onChange={handleInputChange}
          onSelect={keepCaretAtEnd}
          onClick={keepCaretAtEnd}
          onFocus={keepCaretAtEnd}
          disabled={disabled}
          aria-label={ariaLabel}
          className="absolute inset-0 w-full h-full opacity-0 cursor-pointer pointer-events-auto"
        />

        {/* 6 Visual Digit Slots */}
        {Array.from({ length: 6 }).map((_, index) => {
          const char = digits[index] || '';
          const isFilled = Boolean(char);
          const isCurrentActive = index === digits.length && !disabled && digits.length < 6;

          return (
            <div
              key={index}
              className={cn(
                'w-11 h-13 sm:w-12 sm:h-14 rounded-xl flex items-center justify-center text-xl font-bold font-mono transition-all duration-150',
                'border-2 bg-white dark:bg-slate-900 shadow-2xs',
                error
                  ? 'border-rose-400 dark:border-rose-500 bg-rose-50/40 dark:bg-rose-950/20 text-rose-600 dark:text-rose-400 ring-2 ring-rose-400/20'
                  : isCurrentActive
                  ? 'border-emerald-500 dark:border-emerald-400 ring-3 ring-emerald-500/20 dark:ring-emerald-400/20'
                  : isFilled
                  ? 'border-slate-400 dark:border-slate-600 text-slate-900 dark:text-white'
                  : 'border-slate-200 dark:border-slate-800 text-slate-400 dark:text-slate-600',
                disabled && 'opacity-60 cursor-not-allowed bg-slate-50 dark:bg-slate-900/50'
              )}
            >
              {isFilled ? (
                mask ? (
                  <span className="w-3 h-3 rounded-full bg-slate-900 dark:bg-slate-100 scale-100 transition-transform duration-100" />
                ) : (
                  char
                )
              ) : isCurrentActive ? (
                <span className="w-0.5 h-6 bg-emerald-500 animate-pulse" />
              ) : (
                <span className="w-1.5 h-1.5 rounded-full bg-slate-200 dark:bg-slate-800" />
              )}
            </div>
          );
        })}
      </div>

      {/* Optional Mask Toggle */}
      {showToggle && onToggleMask && (
        <button
          type="button"
          onClick={onToggleMask}
          className="flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200 transition pt-1"
        >
          {mask ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
          <span>{mask ? t('security.showPin') : t('security.hidePin')}</span>
        </button>
      )}
    </div>
  );
};
