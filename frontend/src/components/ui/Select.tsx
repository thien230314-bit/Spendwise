import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, Check } from 'lucide-react';
import { cn } from '../../utils/cn';
import { useI18n } from '../../i18n/I18nContext';

export interface SelectOption<T extends string = string> {
  value: T;
  label: React.ReactNode;
  sublabel?: React.ReactNode;
  icon?: React.ReactNode;
  disabled?: boolean;
}

export interface SelectProps<T extends string = string> {
  id?: string;
  name?: string;
  value: T;
  onChange: (value: T) => void;
  options?: SelectOption<T>[];
  children?: React.ReactNode;
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  className?: string;
  triggerClassName?: string;
  menuClassName?: string;
  size?: 'sm' | 'md' | 'lg';
  variant?: 'default' | 'filter' | 'compact';
  accentColor?: 'emerald' | 'rose' | 'purple' | 'amber' | 'blue';
  icon?: React.ReactNode;
  align?: 'left' | 'right';
  'aria-label'?: string;
}

export function Select<T extends string = string>({
  id,
  name,
  value,
  onChange,
  options,
  children,
  placeholder,
  disabled = false,
  required = false,
  className,
  triggerClassName,
  menuClassName,
  size = 'md',
  variant = 'default',
  accentColor = 'emerald',
  icon,
  align = 'left',
  'aria-label': ariaLabel,
}: SelectProps<T>) {
  const { locale } = useI18n();
  const displayPlaceholder =
    placeholder !== undefined
      ? placeholder
      : locale === 'vi-VN'
        ? 'Chọn một tùy chọn'
        : 'Select an option';
  const [isOpen, setIsOpen] = useState(false);
  const [openUpwards, setOpenUpwards] = useState(false);
  const [highlightIndex, setHighlightIndex] = useState<number>(-1);
  const [menuStyle, setMenuStyle] = useState<React.CSSProperties>({});

  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const optionRefs = useRef<(HTMLDivElement | null)[]>([]);

  // Parse options from either `options` prop or `<option>` children
  const parsedOptions: SelectOption<T>[] = useMemo(() => {
    if (options && options.length > 0) return options;
    if (!children) return [];
    const list: SelectOption<T>[] = [];
    React.Children.forEach(children, (child) => {
      if (React.isValidElement(child) && child.type === 'option') {
        list.push({
          value: (child.props.value !== undefined ? String(child.props.value) : '') as T,
          label: child.props.children,
          disabled: Boolean(child.props.disabled),
        });
      }
    });
    return list;
  }, [options, children]);

  // Find currently selected option
  const selectedOption = useMemo(() => {
    return parsedOptions.find((opt) => opt.value === value);
  }, [parsedOptions, value]);

  // Calculate position of the floating menu escaping any modal/container overflow
  const updatePosition = useCallback(() => {
    if (!triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Ignore unmeasured/hidden triggers
    if (rect.width === 0 && rect.height === 0) return;

    // If trigger scrolled completely out of view, close
    if (rect.bottom < 0 || rect.top > viewportHeight) {
      setIsOpen(false);
      return;
    }

    const estimatedMenuHeight = 240;
    const spaceBelow = viewportHeight - rect.bottom;
    const spaceAbove = rect.top;

    // Open upwards if space below is too tight and space above is larger
    const isUpwards = spaceBelow < estimatedMenuHeight && spaceAbove > spaceBelow;
    setOpenUpwards(isUpwards);

    const style: React.CSSProperties = {
      position: 'fixed',
      zIndex: 99999, // Ensure menu sits safely above all modal and dialog backdrops
    };

    // Available height clamped between 120px and 240px
    const availableSpace = isUpwards ? spaceAbove - 16 : spaceBelow - 16;
    const maxHeight = Math.max(120, Math.min(240, availableSpace));
    style.maxHeight = `${maxHeight}px`;

    // Vertical positioning
    if (isUpwards) {
      style.bottom = `${Math.round(viewportHeight - rect.top + 6)}px`;
    } else {
      style.top = `${Math.round(rect.bottom + 6)}px`;
    }

    // Horizontal positioning & sizing
    if (variant === 'filter') {
      style.minWidth = '180px';
      style.maxWidth = `${Math.min(360, viewportWidth - 24)}px`;
      if (align === 'right') {
        const rightVal = Math.round(viewportWidth - rect.right);
        style.right = `${Math.max(8, rightVal)}px`;
        style.left = 'auto';
      } else {
        const leftVal = Math.round(Math.max(8, Math.min(rect.left, viewportWidth - 190)));
        style.left = `${leftVal}px`;
        style.right = 'auto';
      }
    } else {
      // Normal form select: precisely preserve trigger width
      const widthVal = Math.round(rect.width);
      style.width = `${widthVal}px`;
      style.minWidth = `${widthVal}px`;

      if (align === 'right') {
        const rightVal = Math.round(viewportWidth - rect.right);
        style.right = `${Math.max(8, rightVal)}px`;
        style.left = 'auto';
      } else {
        const leftVal = Math.round(Math.max(8, Math.min(rect.left, viewportWidth - widthVal - 8)));
        style.left = `${leftVal}px`;
        style.right = 'auto';
      }
    }

    setMenuStyle(style);
  }, [variant, align]);

  // Click outside listener (respects both trigger container and portaled menu)
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent | TouchEvent) => {
      const target = e.target as Node;
      if (
        containerRef.current &&
        !containerRef.current.contains(target) &&
        menuRef.current &&
        !menuRef.current.contains(target)
      ) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('touchstart', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('touchstart', handleClickOutside);
    };
  }, [isOpen]);

  // Recalculate position on open, resize, and scrolling within any container/modal
  useEffect(() => {
    if (!isOpen) return;

    updatePosition();

    const handleScrollOrResize = () => {
      updatePosition();
    };

    window.addEventListener('resize', handleScrollOrResize, { passive: true });
    // Use capture phase so scrolling inside any overflow-y-auto modal/container triggers position update
    window.addEventListener('scroll', handleScrollOrResize, { capture: true, passive: true });

    return () => {
      window.removeEventListener('resize', handleScrollOrResize);
      window.removeEventListener('scroll', handleScrollOrResize, { capture: true } as EventListenerOptions);
    };
  }, [isOpen, updatePosition]);

  // Sync highlight with selected value when opened
  useEffect(() => {
    if (isOpen) {
      const idx = parsedOptions.findIndex((opt) => opt.value === value);
      const initialIdx = idx >= 0 ? idx : 0;
      setHighlightIndex(initialIdx);
      setTimeout(() => {
        optionRefs.current[initialIdx]?.scrollIntoView({ block: 'nearest' });
      }, 0);
    }
  }, [isOpen, parsedOptions, value]);

  const openMenu = () => {
    if (disabled) return;
    updatePosition();
    setIsOpen(true);
  };

  const closeMenu = () => {
    setIsOpen(false);
  };

  const handleToggle = () => {
    if (disabled) return;
    if (isOpen) {
      closeMenu();
    } else {
      openMenu();
    }
  };

  const handleSelectOption = (opt: SelectOption<T>) => {
    if (opt.disabled || disabled) return;
    onChange(opt.value);
    closeMenu();
    triggerRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (disabled) return;

    if (e.key === ' ' || e.key === 'Enter') {
      e.preventDefault();
      if (!isOpen) {
        openMenu();
      } else if (highlightIndex >= 0 && highlightIndex < parsedOptions.length) {
        const target = parsedOptions[highlightIndex];
        if (!target.disabled) {
          handleSelectOption(target);
        }
      }
      return;
    }

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (!isOpen) {
        openMenu();
        return;
      }
      let nextIndex = highlightIndex + 1;
      while (nextIndex < parsedOptions.length && parsedOptions[nextIndex]?.disabled) {
        nextIndex++;
      }
      if (nextIndex < parsedOptions.length) {
        setHighlightIndex(nextIndex);
        optionRefs.current[nextIndex]?.scrollIntoView({ block: 'nearest' });
      }
      return;
    }

    if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (!isOpen) {
        openMenu();
        return;
      }
      let prevIndex = highlightIndex - 1;
      while (prevIndex >= 0 && parsedOptions[prevIndex]?.disabled) {
        prevIndex--;
      }
      if (prevIndex >= 0) {
        setHighlightIndex(prevIndex);
        optionRefs.current[prevIndex]?.scrollIntoView({ block: 'nearest' });
      }
      return;
    }

    if (e.key === 'Escape') {
      if (isOpen) {
        e.preventDefault();
        closeMenu();
        triggerRef.current?.focus();
      }
      return;
    }

    if (e.key === 'Tab') {
      if (isOpen) {
        closeMenu();
      }
    }
  };

  // Theme-aware styles for accent colors
  const accentClasses = {
    emerald: {
      focusRing: 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-emerald-600 dark:focus-visible:outline-emerald-500 focus-visible:outline-offset-2',
      activeTrigger: 'border-emerald-500 dark:border-emerald-500 ring-2 ring-emerald-500/20',
      selectedOption: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300 font-bold',
      checkIcon: 'text-emerald-600 dark:text-emerald-400',
    },
    rose: {
      focusRing: 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-rose-600 dark:focus-visible:outline-rose-500 focus-visible:outline-offset-2',
      activeTrigger: 'border-rose-500 dark:border-rose-500 ring-2 ring-rose-500/20',
      selectedOption: 'bg-rose-50 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300 font-bold',
      checkIcon: 'text-rose-600 dark:text-rose-400',
    },
    purple: {
      focusRing: 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-purple-600 dark:focus-visible:outline-purple-500 focus-visible:outline-offset-2',
      activeTrigger: 'border-purple-500 dark:border-purple-500 ring-2 ring-purple-500/20',
      selectedOption: 'bg-purple-50 text-purple-700 dark:bg-purple-950/50 dark:text-purple-300 font-bold',
      checkIcon: 'text-purple-600 dark:text-purple-400',
    },
    amber: {
      focusRing: 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-amber-600 dark:focus-visible:outline-amber-500 focus-visible:outline-offset-2',
      activeTrigger: 'border-amber-500 dark:border-amber-500 ring-2 ring-amber-500/20',
      selectedOption: 'bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300 font-bold',
      checkIcon: 'text-amber-600 dark:text-amber-400',
    },
    blue: {
      focusRing: 'focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600 dark:focus-visible:outline-blue-500 focus-visible:outline-offset-2',
      activeTrigger: 'border-blue-500 dark:border-blue-500 ring-2 ring-blue-500/20',
      selectedOption: 'bg-blue-50 text-blue-700 dark:bg-blue-950/50 dark:text-blue-300 font-bold',
      checkIcon: 'text-blue-600 dark:text-blue-400',
    },
  }[accentColor];

  // Base trigger styling based on variant & size
  let triggerBase = '';
  if (variant === 'filter') {
    triggerBase =
      'px-3 py-1.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 text-xs font-semibold hover:border-slate-300 dark:hover:border-slate-600 transition-all flex items-center gap-1.5 cursor-pointer whitespace-nowrap select-none';
  } else if (variant === 'compact' || size === 'sm') {
    triggerBase =
      'w-full h-9 px-3 text-xs font-medium rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 hover:border-slate-300 dark:hover:border-slate-700 transition-all flex items-center justify-between gap-2 cursor-pointer whitespace-nowrap select-none';
  } else {
    // Default form input size matching SpendWise rounded-2xl inputs
    triggerBase =
      'w-full px-3.5 py-2.5 min-h-[42px] rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-semibold text-xs sm:text-sm hover:border-slate-300 dark:hover:border-slate-600 transition-all flex items-center justify-between gap-2 cursor-pointer select-none';
  }

  return (
    <div
      ref={containerRef}
      className={cn(
        'relative',
        variant === 'default' ? 'w-full' : 'inline-block',
        className
      )}
    >
      {/* Hidden input for form data accessibility */}
      {name && <input type="hidden" name={name} value={value} required={required} />}

      <button
        ref={triggerRef}
        id={id}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={ariaLabel}
        onClick={handleToggle}
        onKeyDown={handleKeyDown}
        className={cn(
          triggerBase,
          accentClasses.focusRing,
          isOpen && accentClasses.activeTrigger,
          disabled && 'opacity-60 cursor-not-allowed pointer-events-none',
          triggerClassName
        )}
      >
        <div className="flex items-center gap-2 min-w-0 flex-1 text-left">
          {icon && <span className="shrink-0 text-slate-400">{icon}</span>}
          <span className="truncate">
            {selectedOption ? selectedOption.label : (
              <span className="text-slate-400 font-normal">{displayPlaceholder}</span>
            )}
          </span>
        </div>

        <ChevronDown
          className={cn(
            'h-3.5 w-3.5 text-slate-400 shrink-0 transition-transform duration-200',
            isOpen && 'rotate-180',
            variant === 'filter' ? 'ml-0.5' : 'ml-1.5'
          )}
        />
      </button>

      {/* Floating Dropdown Panel via Portal escaping all overflow / clipping */}
      {isOpen &&
        typeof document !== 'undefined' &&
        createPortal(
          <div
            ref={menuRef}
            role="listbox"
            tabIndex={-1}
            style={menuStyle}
            className={cn(
              'p-1.5 rounded-2xl border border-slate-200/90 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xl shadow-slate-900/10 dark:shadow-slate-950/40 overflow-y-auto space-y-0.5 focus:outline-none animate-in fade-in-0 zoom-in-95 duration-100',
              openUpwards ? 'origin-bottom' : 'origin-top',
              variant === 'filter' ? 'whitespace-normal' : '',
              menuClassName
            )}
          >
            {parsedOptions.length === 0 ? (
              <div className="px-3 py-2.5 text-xs text-slate-400 dark:text-slate-500 text-center font-medium">
                {locale === 'vi-VN' ? 'Không có lựa chọn phù hợp' : 'No options available'}
              </div>
            ) : (
              parsedOptions.map((opt, idx) => {
                const isSelected = opt.value === value;
                const isHighlighted = idx === highlightIndex;

                return (
                  <div
                    key={`${opt.value}-${idx}`}
                    ref={(el) => {
                      optionRefs.current[idx] = el;
                    }}
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => handleSelectOption(opt)}
                    onMouseEnter={() => setHighlightIndex(idx)}
                    className={cn(
                      'px-3 py-2 text-xs sm:text-sm rounded-xl flex items-center justify-between gap-2.5 cursor-pointer transition-colors text-left select-none',
                      opt.disabled && 'opacity-40 cursor-not-allowed pointer-events-none',
                      isSelected
                        ? accentClasses.selectedOption
                        : isHighlighted
                        ? 'bg-slate-100 dark:bg-slate-800/80 text-slate-900 dark:text-white'
                        : 'text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800/80'
                    )}
                  >
                    <div className="flex items-center gap-2 min-w-0 flex-1">
                      {opt.icon && <span className="shrink-0">{opt.icon}</span>}
                      <div className="flex flex-col min-w-0">
                        <span className="truncate leading-tight">{opt.label}</span>
                        {opt.sublabel && (
                          <span className="text-[11px] text-slate-400 dark:text-slate-500 font-normal truncate">
                            {opt.sublabel}
                          </span>
                        )}
                      </div>
                    </div>

                    {isSelected && (
                      <Check className={cn('h-4 w-4 shrink-0', accentClasses.checkIcon)} />
                    )}
                  </div>
                );
              })
            )}
          </div>,
          document.body
        )}
    </div>
  );
}
