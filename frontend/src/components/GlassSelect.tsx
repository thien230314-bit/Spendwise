import React, { useEffect, useId, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  Check,
  ChevronDown,
  Landmark,
  ReceiptText,
  Search,
  Tag,
  Wallet,
  X,
} from 'lucide-react';
import { motion } from 'motion/react';
import { useSpendWise } from '../context/SpendWiseContext';
import { UserAvatar } from './UserAvatar';
import { useModalFocusTrap } from './useModalFocusTrap';

export interface GlassSelectOption {
  value: string;
  label: string;
  /** Secondary line of text (e.g. phone · role, available balance, masked bank account, tx code + amount) */
  sublabel?: string;
  /** Optional small badge rendered next to the main label (e.g. "Chính bạn") */
  badge?: string;
  badgeTone?: 'ok' | 'fix' | 'free' | 'wait' | 'bad';
  /** Optional avatar info for recipient/member items */
  avatar?: {
    fullName: string;
    initials?: string;
    avatarUrl?: string | null;
    gradient?: string;
  };
  /** Optional icon type for wallet / bank / category / transaction items */
  icon?: 'wallet' | 'bank' | 'category' | 'transaction' | 'none';
  disabled?: boolean;
  /** Optional extra keywords for display-only search filtering */
  searchText?: string;
}

export interface GlassSelectProps {
  id?: string;
  name?: string;
  value: string;
  onValueChange: (nextValue: string) => void;
  options: GlassSelectOption[];
  placeholder?: string;
  sheetTitle?: string;
  sheetSubtitle?: string;
  disabled?: boolean;
  required?: boolean;
  hasError?: boolean;
  /** Override whether the search input appears inside the sheet (defaults to options.length >= 6) */
  searchable?: boolean;
  searchPlaceholder?: string;
  /** Whether to show the option sublabel inside the closed trigger button (defaults to true) */
  showTriggerSublabel?: boolean;
  className?: string;
  'aria-label'?: string;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
}

function normalizeForSearch(str: string): string {
  return str
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

export const GlassSelect: React.FC<GlassSelectProps> = ({
  id,
  name,
  value,
  onValueChange,
  options,
  placeholder,
  sheetTitle,
  sheetSubtitle,
  disabled = false,
  required = false,
  hasError = false,
  searchable,
  searchPlaceholder,
  showTriggerSublabel = true,
  className = '',
  'aria-label': ariaLabel,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}) => {
  const { tr } = useSpendWise();
  const generatedId = useId();
  const controlId = id || generatedId;
  const titleId = useId();
  const subtitleId = useId();
  const listboxId = useId();

  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [portalTarget, setPortalTarget] = useState<HTMLElement | null>(null);

  const triggerBtnRef = useRef<HTMLButtonElement>(null);
  const closeBtnRef = useRef<HTMLButtonElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const selectedOptionBtnRef = useRef<HTMLButtonElement | null>(null);
  const optionBtnRefs = useRef<Array<HTMLButtonElement | null>>([]);

  useEffect(() => {
    const target =
      document.getElementById('sw-overlay-root') ||
      document.getElementById('sw-phone-shell') ||
      document.body;
    setPortalTarget(target);
  }, []);

  const selectedOption = useMemo(
    () => options.find((opt) => opt.value === value) || null,
    [options, value]
  );

  const isSearchEnabled = searchable ?? options.length >= 6;

  const filteredOptions = useMemo(() => {
    const q = normalizeForSearch(searchQuery);
    if (!q) return options;
    return options.filter((opt) => {
      const haystack = normalizeForSearch(
        [opt.label, opt.sublabel, opt.badge, opt.searchText].filter(Boolean).join(' ')
      );
      return haystack.includes(q);
    });
  }, [options, searchQuery]);

  const handleClose = () => {
    setIsOpen(false);
    setSearchQuery('');
  };

  const handleOpen = () => {
    if (disabled) return;
    setSearchQuery('');
    setIsOpen(true);
  };

  const { dialogRef } = useModalFocusTrap({
    isOpen: isOpen && Boolean(portalTarget),
    initialFocusRef: selectedOption ? selectedOptionBtnRef : closeBtnRef,
    returnFocusRef: triggerBtnRef,
    onClose: handleClose,
  });

  useEffect(() => {
    if (!isOpen) return;
    const raf = window.requestAnimationFrame(() => {
      if (selectedOptionBtnRef.current) {
        selectedOptionBtnRef.current.scrollIntoView({ block: 'nearest' });
      }
    });
    return () => window.cancelAnimationFrame(raf);
  }, [isOpen]);

  const handleSelectOption = (opt: GlassSelectOption) => {
    if (opt.disabled) return;
    onValueChange(opt.value);
    handleClose();
  };

  const handleTriggerKeyDown = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    if (disabled) return;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      handleOpen();
    }
  };

  const handleSheetListKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    const enabledIndices = filteredOptions
      .map((opt, idx) => (opt.disabled ? -1 : idx))
      .filter((idx) => idx !== -1);
    if (enabledIndices.length === 0) return;

    const currentFocusedIdx = optionBtnRefs.current.findIndex(
      (btn) => btn && btn === document.activeElement
    );

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const pos = enabledIndices.indexOf(currentFocusedIdx);
      const nextIdx =
        pos === -1 || pos === enabledIndices.length - 1
          ? enabledIndices[0]
          : enabledIndices[pos + 1];
      optionBtnRefs.current[nextIdx]?.focus();
      optionBtnRefs.current[nextIdx]?.scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const pos = enabledIndices.indexOf(currentFocusedIdx);
      const prevIdx =
        pos <= 0 ? enabledIndices[enabledIndices.length - 1] : enabledIndices[pos - 1];
      optionBtnRefs.current[prevIdx]?.focus();
      optionBtnRefs.current[prevIdx]?.scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Home') {
      e.preventDefault();
      const firstIdx = enabledIndices[0];
      optionBtnRefs.current[firstIdx]?.focus();
      optionBtnRefs.current[firstIdx]?.scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'End') {
      e.preventDefault();
      const lastIdx = enabledIndices[enabledIndices.length - 1];
      optionBtnRefs.current[lastIdx]?.focus();
      optionBtnRefs.current[lastIdx]?.scrollIntoView({ block: 'nearest' });
    }
  };

  const renderLeadingVisual = (opt: GlassSelectOption, isCompactTrigger = false) => {
    if (opt.avatar) {
      return (
        <UserAvatar
          fullName={opt.avatar.fullName}
          initials={opt.avatar.initials}
          avatarUrl={opt.avatar.avatarUrl}
          gradient={opt.avatar.gradient}
          size="sm"
          className={
            isCompactTrigger
              ? '!h-8 !w-8 !rounded-[11px] !text-[0.75rem] shrink-0'
              : '!h-10 !w-10 !rounded-[13px] !text-[0.8125rem] shrink-0'
          }
        />
      );
    }

    if (opt.icon === 'wallet') {
      return (
        <span
          aria-hidden="true"
          className={`sw-ti shrink-0 ${
            isCompactTrigger ? '!h-8 !w-8 !rounded-[11px]' : '!h-10 !w-10 !rounded-[13px]'
          }`}
        >
          <Wallet className="h-4 w-4" />
        </span>
      );
    }

    if (opt.icon === 'bank') {
      return (
        <span
          aria-hidden="true"
          className={`sw-ti blue shrink-0 ${
            isCompactTrigger ? '!h-8 !w-8 !rounded-[11px]' : '!h-10 !w-10 !rounded-[13px]'
          }`}
        >
          <Landmark className="h-4 w-4" />
        </span>
      );
    }

    if (opt.icon === 'transaction') {
      return (
        <span
          aria-hidden="true"
          className={`sw-ti pend shrink-0 ${
            isCompactTrigger ? '!h-8 !w-8 !rounded-[11px]' : '!h-10 !w-10 !rounded-[13px]'
          }`}
        >
          <ReceiptText className="h-4 w-4" />
        </span>
      );
    }

    if (opt.icon === 'category') {
      return (
        <span
          aria-hidden="true"
          className={`sw-ti vio shrink-0 ${
            isCompactTrigger ? '!h-8 !w-8 !rounded-[11px]' : '!h-10 !w-10 !rounded-[13px]'
          }`}
        >
          <Tag className="h-4 w-4" />
        </span>
      );
    }

    return null;
  };

  const renderSublabel = (sublabel: string, isTrigger = false) => {
    const parts = sublabel
      .split(' · ')
      .map((p) => p.trim())
      .filter(Boolean);

    if (parts.length > 1) {
      return (
        <span
          className={`mt-0.5 flex flex-col gap-0.5 text-[0.8125rem] ${
            isTrigger ? 'leading-[1.35]' : 'leading-[1.38]'
          } font-medium text-[var(--ink2)] tabular-nums`}
        >
          {parts.map((part, idx) => {
            const isPhoneLike = /^[+0-9][0-9\s.-]{6,}$/.test(part);
            return (
              <React.Fragment key={idx}>
                {idx > 0 && ' '}
                <span
                  className={
                    isPhoneLike
                      ? 'block whitespace-nowrap tabular-nums'
                      : 'block break-words [word-break:normal] [overflow-wrap:break-word]'
                  }
                >
                  {part}
                </span>
              </React.Fragment>
            );
          })}
        </span>
      );
    }

    const isSinglePhoneLike = /^[+0-9][0-9\s.-]{6,}$/.test(sublabel.trim());
    return (
      <span
        className={`mt-0.5 block text-[0.8125rem] ${
          isTrigger ? 'leading-[1.35]' : 'leading-[1.38]'
        } font-medium text-[var(--ink2)] tabular-nums ${
          isSinglePhoneLike
            ? 'whitespace-nowrap'
            : 'break-words [word-break:normal] [overflow-wrap:break-word]'
        }`}
      >
        {sublabel}
      </span>
    );
  };

  const isInvalid = Boolean(ariaInvalid || hasError);
  const resolvedSheetTitle =
    sheetTitle || ariaLabel || tr('Chọn một mục', 'Select an option');

  return (
    <div className="relative w-full min-w-0">
      {name && (
        <input
          type="hidden"
          name={name}
          value={value}
          required={required}
          disabled={disabled}
        />
      )}

      {/* Closed Trigger Control */}
      <button
        ref={triggerBtnRef}
        id={controlId}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={isOpen ? listboxId : undefined}
        aria-label={ariaLabel}
        aria-invalid={isInvalid || undefined}
        aria-describedby={ariaDescribedBy}
        onClick={handleOpen}
        onKeyDown={handleTriggerKeyDown}
        className={`sw-focus-ring w-full min-h-[52px] rounded-[18px] bg-[var(--sw-surface-solid-fallback)] bg-[var(--glass2)] border-[1.5px] px-3.5 py-2.5 text-left transition-all flex items-center justify-between gap-2.5 ${
          isInvalid
            ? '!border-[var(--rose)] focus:ring-4 focus:ring-[var(--rose)]/15'
            : isOpen
            ? 'border-[var(--em)] ring-4 ring-[var(--em)]/15'
            : 'border-[var(--stroke)] hover:border-[var(--sw-border-strong)] focus:border-[var(--em)] focus:ring-4 focus:ring-[var(--em)]/15'
        } ${
          disabled
            ? 'opacity-55 cursor-not-allowed bg-[var(--sw-disabled-bg)] text-[var(--sw-disabled-text)]'
            : 'cursor-pointer text-[var(--ink)]'
        } ${className}`}
      >
        {selectedOption ? (
          <span className="flex items-center gap-2.5 min-w-0 flex-1">
            {renderLeadingVisual(selectedOption, true)}
            <span className="min-w-0 flex-1 block">
              <span className="flex flex-wrap items-center gap-1.5 min-w-0">
                <span className="text-[0.9375rem] leading-[1.35] font-bold text-[var(--ink)] break-words [word-break:normal] [overflow-wrap:break-word]">
                  {selectedOption.label}
                </span>
                {selectedOption.badge && (
                  <span
                    className={`sw-badge ${
                      selectedOption.badgeTone ? `b-${selectedOption.badgeTone}` : 'b-ok'
                    } !text-[0.6875rem] !px-2 !py-0.5`}
                  >
                    {selectedOption.badge}
                  </span>
                )}
              </span>
              {showTriggerSublabel &&
                selectedOption.sublabel &&
                renderSublabel(selectedOption.sublabel, true)}
            </span>
          </span>
        ) : (
          <span className="min-w-0 flex-1 text-[0.9375rem] leading-[1.35] font-medium text-[var(--ink3)] break-words">
            {placeholder || tr('Chọn một mục...', 'Select an option...')}
          </span>
        )}

        <ChevronDown
          aria-hidden="true"
          className={`h-5 w-5 shrink-0 transition-transform duration-200 ${
            isOpen ? 'rotate-180 text-[var(--em)]' : 'text-[var(--ink2)]'
          }`}
        />
      </button>

      {/* Open Bottom Sheet inside Mobile App Shell */}
      {isOpen &&
        portalTarget &&
        createPortal(
          <div
            data-sw-sheet-open="true"
            className="sw-ov pointer-events-auto"
            style={{ zIndex: 130 }}
          >
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ duration: 0.15 }}
              onClick={handleClose}
              aria-hidden="true"
              className="absolute inset-0"
            />

            {/* Sheet Panel */}
            <motion.div
              ref={dialogRef}
              role="dialog"
              aria-modal="true"
              aria-labelledby={titleId}
              aria-describedby={sheetSubtitle ? subtitleId : undefined}
              tabIndex={-1}
              onKeyDown={handleSheetListKeyDown}
              initial={{ y: '100%' }}
              animate={{ y: 0 }}
              transition={{ type: 'spring', damping: 30, stiffness: 360 }}
              className="sw-sheet !p-0 focus:outline-none"
            >
              {/* Sheet Header */}
              <div className="sw-sheet-header shrink-0 px-5 pt-2.5 pb-3 border-b border-[var(--sw-stroke-s)] bg-[var(--sw-sheet-bg)]">
                <div className="sw-grab" aria-hidden="true" />
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1 pr-1">
                    <h2
                      id={titleId}
                      className="font-display text-[1.125rem] leading-[1.28] font-extrabold text-[var(--sw-ink)] break-words"
                    >
                      {resolvedSheetTitle}
                    </h2>
                    {sheetSubtitle && (
                      <p
                        id={subtitleId}
                        className="mt-1 text-[0.84375rem] leading-[1.4] text-[var(--sw-ink-2)] break-words"
                      >
                        {sheetSubtitle}
                      </p>
                    )}
                  </div>

                  <button
                    ref={closeBtnRef}
                    type="button"
                    onClick={handleClose}
                    aria-label={tr('Đóng danh sách chọn', 'Close selection sheet')}
                    className="sw-focus-ring sw-ib shrink-0 !h-11 !w-11 !min-h-[44px] !min-w-[44px]"
                  >
                    <X aria-hidden="true" className="h-5 w-5" />
                  </button>
                </div>

                {/* Display-only Quick Search for Long Lists */}
                {isSearchEnabled && options.length > 0 && (
                  <div className="sw-search mt-3">
                    <Search
                      aria-hidden="true"
                      className="h-4 w-4 shrink-0 text-[var(--ink3)]"
                    />
                    <input
                      ref={searchInputRef}
                      type="text"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder={
                        searchPlaceholder ||
                        tr('Tìm theo tên, số điện thoại, mã...', 'Search by name, phone, code...')
                      }
                      aria-label={tr('Tìm kiếm trong danh sách', 'Search options')}
                    />
                    {searchQuery && (
                      <button
                        type="button"
                        onClick={() => {
                          setSearchQuery('');
                          searchInputRef.current?.focus();
                        }}
                        aria-label={tr('Xóa từ khóa', 'Clear search')}
                        className="sw-focus-ring inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--line)] text-[var(--ink2)] hover:text-[var(--ink)]"
                      >
                        <X aria-hidden="true" className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>
                )}
              </div>

              {/* Scrollable Options Body */}
              <div className="sw-sheet-body flex-1 min-h-0 overflow-y-auto overscroll-contain px-4 py-3.5 thin-scrollbar bg-[var(--sw-sheet-bg)] pb-[calc(1.25rem+env(safe-area-inset-bottom,0px))]">
                {options.length === 0 ? (
                  <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-5 text-center text-[0.875rem] font-medium text-[var(--ink2)]">
                    {tr('Chưa có lựa chọn nào khả dụng.', 'No options available.')}
                  </div>
                ) : filteredOptions.length === 0 ? (
                  <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-5 text-center space-y-3">
                    <p className="text-[0.9375rem] font-bold text-[var(--ink)]">
                      {tr('Không tìm thấy kết quả', 'No results found')}
                    </p>
                    <button
                      type="button"
                      onClick={() => {
                        setSearchQuery('');
                        searchInputRef.current?.focus();
                      }}
                      className="sw-focus-ring sw-btn ghost sm"
                    >
                      <span>{tr('Xóa từ khóa', 'Clear search')}</span>
                    </button>
                  </div>
                ) : (
                  <div
                    id={listboxId}
                    role="listbox"
                    aria-labelledby={titleId}
                    className="space-y-2"
                  >
                    {filteredOptions.map((opt, idx) => {
                      const isSelected = opt.value === value;
                      return (
                        <button
                          key={opt.value || `__empty_${idx}`}
                          ref={(el) => {
                            optionBtnRefs.current[idx] = el;
                            if (isSelected) {
                              selectedOptionBtnRef.current = el;
                            }
                          }}
                          type="button"
                          role="option"
                          aria-selected={isSelected}
                          disabled={opt.disabled}
                          onClick={() => handleSelectOption(opt)}
                          className={`sw-focus-ring w-full min-h-[52px] rounded-[18px] px-3.5 py-3 text-left transition-all flex items-center justify-between gap-3 ${
                            opt.disabled
                              ? 'opacity-50 cursor-not-allowed bg-[var(--sw-disabled-bg)] border-[1.5px] border-[var(--line)]'
                              : isSelected
                              ? 'bg-[var(--sw-selected-bg)] border-[1.5px] border-[var(--sw-selected-border)] shadow-2xs cursor-pointer'
                              : 'bg-[var(--glass2)] border-[1.5px] border-[var(--line)] hover:border-[var(--em)]/55 active:scale-[0.99] cursor-pointer'
                          }`}
                        >
                          <span className="flex items-center gap-3 min-w-0 flex-1">
                            {renderLeadingVisual(opt, false)}
                            <span className="min-w-0 flex-1 block">
                              <span className="flex flex-wrap items-center gap-1.5 min-w-0">
                                <span
                                  className={`text-[0.9375rem] leading-[1.35] break-words [word-break:normal] [overflow-wrap:break-word] ${
                                    isSelected
                                      ? 'font-extrabold text-[var(--ink)]'
                                      : 'font-bold text-[var(--ink)]'
                                  }`}
                                >
                                  {opt.label}
                                </span>
                                {opt.badge && (
                                  <span
                                    className={`sw-badge ${
                                      opt.badgeTone ? `b-${opt.badgeTone}` : 'b-ok'
                                    } !text-[0.6875rem] !px-2 !py-0.5`}
                                  >
                                    {opt.badge}
                                  </span>
                                )}
                              </span>
                              {opt.sublabel && renderSublabel(opt.sublabel, false)}
                            </span>
                          </span>

                          {/* Selection Indicator (Checkmark + Ring, not color alone) */}
                          <span
                            aria-hidden="true"
                            className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors ${
                              isSelected
                                ? 'bg-[var(--em)] text-white shadow-2xs'
                                : 'border-[1.5px] border-[var(--stroke)] bg-transparent'
                            }`}
                          >
                            {isSelected && <Check className="h-3.5 w-3.5 stroke-[2.75]" />}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </motion.div>
          </div>,
          portalTarget
        )}
    </div>
  );
};
