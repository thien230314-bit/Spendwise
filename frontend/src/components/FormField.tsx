import React, { useId } from 'react';
import { AlertCircle } from 'lucide-react';
import { GlassSelect } from './GlassSelect';
export type { GlassSelectOption, GlassSelectProps } from './GlassSelect';
export { GlassSelect };

interface FormFieldProps {
  label: string;
  hint?: string;
  error?: string;
  children: React.ReactNode;
  rightLabel?: React.ReactNode;
  htmlFor?: string;
  required?: boolean;
}

function injectFormControlProps(
  node: React.ReactNode,
  controlProps: {
    id: string;
    label: string;
    'aria-invalid': boolean;
    'aria-describedby'?: string;
  }
): { node: React.ReactNode; injected: boolean } {
  if (!React.isValidElement(node)) {
    if (Array.isArray(node)) {
      let done = false;
      const mapped = node.map((item, idx) => {
        if (done) return item;
        const res = injectFormControlProps(item, controlProps);
        if (res.injected) {
          done = true;
          return React.isValidElement(res.node) && res.node.key == null
            ? React.cloneElement(res.node, { key: idx })
            : res.node;
        }
        return item;
      });
      return { node: mapped, injected: done };
    }
    return { node, injected: false };
  }

  const el = node as React.ReactElement<Record<string, unknown>>;
  const isIntrinsicControl =
    typeof el.type === 'string' &&
    (el.type === 'input' || el.type === 'select' || el.type === 'textarea');
  const isCustomControl = el.type === CurrencyInput || el.type === GlassSelect;

  if (isIntrinsicControl || isCustomControl) {
    const existingId = typeof el.props.id === 'string' && el.props.id ? el.props.id : controlProps.id;
    const existingDesc =
      typeof el.props['aria-describedby'] === 'string' ? el.props['aria-describedby'] : undefined;
    const mergedDesc = [existingDesc, controlProps['aria-describedby']]
      .filter(Boolean)
      .join(' ') || undefined;
    const existingInvalid = Boolean(el.props['aria-invalid']);

    const extraProps: Record<string, unknown> = {
      id: existingId,
      'aria-invalid': existingInvalid || controlProps['aria-invalid'],
      'aria-describedby': mergedDesc,
    };

    if (el.type === GlassSelect) {
      if (!el.props.sheetTitle) {
        extraProps.sheetTitle = controlProps.label;
      }
      if (!el.props['aria-label']) {
        extraProps['aria-label'] = controlProps.label;
      }
    }

    return {
      node: React.cloneElement(el, extraProps),
      injected: true,
    };
  }

  if (el.props && el.props.children) {
    const childResult = injectFormControlProps(
      el.props.children as React.ReactNode,
      controlProps
    );
    if (childResult.injected) {
      return {
        node: React.cloneElement(el, { children: childResult.node }),
        injected: true,
      };
    }
  }

  return { node, injected: false };
}

export const FormField: React.FC<FormFieldProps> = ({
  label,
  hint,
  error,
  children,
  rightLabel,
  htmlFor,
}) => {
  const generatedId = useId();
  const fieldId = htmlFor || generatedId;
  const descId = `${fieldId}-desc`;

  const { node: enhancedChild } = injectFormControlProps(children, {
    id: fieldId,
    label,
    'aria-invalid': Boolean(error),
    'aria-describedby': error || hint ? descId : undefined,
  });

  return (
    <div className="flex h-full flex-col gap-1.5">
      <div className="mb-auto flex flex-wrap items-center justify-between gap-2 px-1">
        <label
          htmlFor={fieldId}
          className="cursor-pointer text-[0.875rem] leading-[1.35] font-bold text-[var(--ink2)]"
        >
          {label}
        </label>
        {rightLabel && (
          <div className="text-[0.875rem] leading-[1.35] font-semibold text-[var(--ink2)]">
            {rightLabel}
          </div>
        )}
      </div>
      {enhancedChild}
      {error ? (
        <div
          id={descId}
          role="alert"
          className="flex items-start gap-1.5 px-1 text-[0.875rem] leading-[1.4] font-semibold text-[var(--rose)]"
        >
          <AlertCircle aria-hidden="true" className="h-4 w-4 shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      ) : hint ? (
        <p id={descId} className="px-1 text-[0.875rem] leading-[1.4] text-[var(--ink2)]">
          {hint}
        </p>
      ) : null}
    </div>
  );
};

/* Reference .inp style with 16px (1rem) font size and 52px min-height for comfortable touch & reading */
export const inputCoreClasses =
  'w-full min-h-[52px] rounded-[18px] bg-[var(--sw-surface-solid-fallback)] bg-[var(--glass2)] border-[1.5px] border-[var(--stroke)] py-2.5 text-[1rem] font-semibold leading-[1.4] text-[var(--ink)] placeholder:text-[var(--ink3)] placeholder:font-medium focus:outline-none focus:border-[var(--em)] focus:ring-4 focus:ring-[var(--em)]/15 transition-all';

export const inputBaseClasses = `${inputCoreClasses} pl-4 pr-4`;

export const glassInputClass = (
  hasLeftIcon = false,
  hasRightControl = false,
  hasError = false
): string =>
  `${inputCoreClasses} ${hasLeftIcon ? 'pl-11' : 'pl-4'} ${hasRightControl ? 'pr-12' : 'pr-4'} ${
    hasError ? '!border-[var(--rose)]' : ''
  }`;

/**
 * Formats a numeric string or number into Vietnamese dot-separated format (e.g. 20000000 -> "20.000.000").
 */
export function formatVndInput(value: string | number): string {
  const digits = String(value ?? '').replace(/\D/g, '');
  if (!digits) return '';
  const normalized = digits.replace(/^0+(?=\d)/, '');
  return normalized.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
}

/**
 * Parses a formatted or raw VND string into an integer number (e.g. "20.000.000" -> 20000000).
 */
export function parseVndInput(value: string | number): number {
  const digits = String(value ?? '').replace(/\D/g, '');
  return digits ? Number(digits) : 0;
}

function findCursorPosByDigitCount(formatted: string, digitCount: number): number {
  if (digitCount <= 0) return 0;
  let seen = 0;
  for (let i = 0; i < formatted.length; i++) {
    if (/\d/.test(formatted[i])) {
      seen++;
      if (seen === digitCount) {
        return i + 1;
      }
    }
  }
  return formatted.length;
}

interface CurrencyInputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> {
  value: string | number;
  onValueChange: (formattedValue: string, numericValue: number) => void;
}

export const CurrencyInput: React.FC<CurrencyInputProps> = ({
  value,
  onValueChange,
  className,
  onKeyDown,
  ...rest
}) => {
  const inputRef = React.useRef<HTMLInputElement>(null);
  const pendingCursorRef = React.useRef<number | null>(null);
  const displayValue = formatVndInput(value);

  React.useLayoutEffect(() => {
    if (
      pendingCursorRef.current !== null &&
      inputRef.current &&
      document.activeElement === inputRef.current
    ) {
      const pos = pendingCursorRef.current;
      pendingCursorRef.current = null;
      inputRef.current.setSelectionRange(pos, pos);
    }
  }, [displayValue]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    onKeyDown?.(e);
    if (e.defaultPrevented) return;

    const el = e.currentTarget;
    const start = el.selectionStart;
    const end = el.selectionEnd;

    // When cursor is collapsed right after or before a '.' separator, delete the adjacent digit cleanly
    if (start !== null && end !== null && start === end) {
      if (e.key === 'Backspace' && start > 0 && displayValue[start - 1] === '.') {
        e.preventDefault();
        const cutIndex = Math.max(0, start - 2);
        const nextRaw = displayValue.slice(0, cutIndex) + displayValue.slice(start);
        const digitsBefore = displayValue.slice(0, cutIndex).replace(/\D/g, '').length;
        const nextFormatted = formatVndInput(nextRaw);
        pendingCursorRef.current = findCursorPosByDigitCount(nextFormatted, digitsBefore);
        onValueChange(nextFormatted, parseVndInput(nextFormatted));
        return;
      }

      if (e.key === 'Delete' && start < displayValue.length && displayValue[start] === '.') {
        e.preventDefault();
        const nextRaw = displayValue.slice(0, start) + displayValue.slice(start + 2);
        const digitsBefore = displayValue.slice(0, start).replace(/\D/g, '').length;
        const nextFormatted = formatVndInput(nextRaw);
        pendingCursorRef.current = findCursorPosByDigitCount(nextFormatted, digitsBefore);
        onValueChange(nextFormatted, parseVndInput(nextFormatted));
        return;
      }
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const rawVal = e.target.value;
    const selStart = e.target.selectionStart ?? rawVal.length;
    const rawBeforeCursor = rawVal.slice(0, selStart).replace(/\D/g, '');
    const allDigits = rawVal.replace(/\D/g, '');
    const normalizedDigits = allDigits.replace(/^0+(?=\d)/, '');
    const leadingZerosRemoved = allDigits.length - normalizedDigits.length;
    const digitsBefore = Math.max(0, rawBeforeCursor.length - leadingZerosRemoved);
    const nextFormatted = formatVndInput(normalizedDigits);

    pendingCursorRef.current = findCursorPosByDigitCount(nextFormatted, digitsBefore);
    onValueChange(nextFormatted, parseVndInput(nextFormatted));
  };

  return (
    <input
      {...rest}
      ref={inputRef}
      type="text"
      inputMode="numeric"
      value={displayValue}
      onKeyDown={handleKeyDown}
      onChange={handleChange}
      className={
        className ||
        `${glassInputClass()} font-display text-[1.0625rem] font-extrabold tabular-nums`
      }
    />
  );
};


