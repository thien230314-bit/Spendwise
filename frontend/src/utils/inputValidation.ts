/**
 * Shared Input Validation and Sanitization Utility
 * System-wide input hardening for SpendWise frontend.
 */

/**
 * Strips all non-digit characters from an input string.
 * Preserves leading zeros (vital for phone numbers, CCCD, bank account numbers).
 */
export function sanitizeDigits(value: unknown): string {
  if (value === null || value === undefined) return '';
  return String(value).replace(/\D/g, '');
}

/**
 * Strips all non-digit characters for numeric input fields.
 * If preserveLeadingZeros is false, strips redundant leading zeros (e.g. '050' -> '50').
 */
export function sanitizeIntegerInput(value: unknown, preserveLeadingZeros = false): string {
  const digits = sanitizeDigits(value);
  if (preserveLeadingZeros) return digits;
  if (!digits) return '';
  // Remove leading zeros unless the string is just '0'
  return digits.replace(/^0+(?=\d)/, '');
}

/**
 * Validates and safely parses a positive integer (e.g., VND amounts > 0).
 * Rejects empty strings, decimals, scientific notation, negative numbers, NaN, Infinity, and unsafe integers.
 */
export function parseSafePositiveInteger(value: unknown): { valid: boolean; value: number } {
  if (value === null || value === undefined || value === '') {
    return { valid: false, value: 0 };
  }

  const str = String(value).trim();
  // Must be strictly digits only (no signs, no decimals, no 'e' notation)
  if (!/^\d+$/.test(str)) {
    return { valid: false, value: 0 };
  }

  const num = Number(str);
  if (!Number.isFinite(num) || !Number.isSafeInteger(num) || num <= 0) {
    return { valid: false, value: 0 };
  }

  return { valid: true, value: num };
}

/**
 * Validates and safely parses a non-negative integer (e.g., threshold >= 0).
 * Rejects empty strings, decimals, scientific notation, negative numbers, NaN, Infinity, and unsafe integers.
 */
export function parseSafeNonNegativeInteger(value: unknown): { valid: boolean; value: number } {
  if (value === null || value === undefined || value === '') {
    return { valid: false, value: 0 };
  }

  const str = String(value).trim();
  if (!/^\d+$/.test(str)) {
    return { valid: false, value: 0 };
  }

  const num = Number(str);
  if (!Number.isFinite(num) || !Number.isSafeInteger(num) || num < 0) {
    return { valid: false, value: 0 };
  }

  return { valid: true, value: num };
}

/**
 * Validates and parses an integer strictly within [min, max] range.
 */
export function parseSafeIntegerInRange(
  value: unknown,
  min: number,
  max: number
): { valid: boolean; value: number } {
  if (value === null || value === undefined || value === '') {
    return { valid: false, value: 0 };
  }

  const str = String(value).trim();
  if (!/^\d+$/.test(str)) {
    return { valid: false, value: 0 };
  }

  const num = Number(str);
  if (!Number.isFinite(num) || !Number.isSafeInteger(num) || num < min || num > max) {
    return { valid: false, value: 0 };
  }

  return { valid: true, value: num };
}

/**
 * Validates and parses a percentage (0 to 100).
 * Allows at most 2 decimal places. Rejects negative, > 100, scientific notation, NaN, Infinity.
 */
export function parseSafePercentage(
  value: unknown,
  maxDecimals = 2
): { valid: boolean; value: number } {
  if (value === null || value === undefined || value === '') {
    return { valid: false, value: 0 };
  }

  const str = String(value).trim();
  // Strictly numeric with optional decimal point up to maxDecimals
  const regex = maxDecimals === 0
    ? /^(?:100|[0-9]{1,2})$/
    : new RegExp(`^(?:100(?:\\.0{1,${maxDecimals}})?|[0-9]{1,2}(?:\\.[0-9]{1,${maxDecimals}})?)$`);

  if (!regex.test(str)) {
    return { valid: false, value: 0 };
  }

  const num = Number(str);
  if (!Number.isFinite(num) || isNaN(num) || num < 0 || num > 100) {
    return { valid: false, value: 0 };
  }

  return { valid: true, value: Number(num.toFixed(maxDecimals)) };
}

/**
 * Sanitizes Vietnam phone number input:
 * - Digits only
 * - Preserves leading zero
 * - DOES NOT truncate to 10 digits (retains over-length digits for strict validation)
 * Validation (isValidVietnamPhone), not sanitization, enforces exactly 10 digits.
 */
export function sanitizeVietnamPhoneInput(value: unknown): string {
  return sanitizeDigits(value);
}

/**
 * Normalizes a Vietnam phone number to canonical digit-only representation.
 */
export function normalizeVietnamPhone(value: unknown): string {
  return sanitizeDigits(value);
}

/**
 * Validates Vietnam phone number format:
 * EXACTLY 10 digits and MUST start with 0 (^0\d{9}$).
 */
export const VIETNAM_PHONE_REGEX = /^0\d{9}$/;

export function isValidVietnamPhone(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  const trimmed = value.trim();
  return VIETNAM_PHONE_REGEX.test(trimmed);
}

/**
 * Sanitizes full name:
 * - Trims leading and trailing whitespace
 * - Collapses repeated internal whitespace to a single space
 */
export function sanitizeFullName(value: unknown): string {
  if (typeof value !== 'string') return '';
  return value.trim().replace(/\s+/g, ' ');
}

/**
 * Validates Full Name:
 * - Minimum 2 characters, maximum 100 characters
 * - Must contain at least one Unicode letter
 * - Must NOT consist entirely of digits or punctuation/symbols
 * - Allows normal letters (including Vietnamese diacritics), spaces, hyphens, apostrophes, and dots
 * - Rejects strings containing digits or disallowed symbols
 */
export function isValidFullName(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  const collapsed = sanitizeFullName(value);
  if (collapsed.length < 2 || collapsed.length > 100) return false;
  if (!/\p{L}/u.test(collapsed)) return false;
  return /^[\p{L}\s'’.-]+$/u.test(collapsed);
}

/**
 * Sensible standard email validation format:
 * - Practical email validation conforming to SpendWise rules
 * - Supports multi-level organizational & educational domains (e.g. tbd.edu.vn, company.com.vn)
 * - Rejects single-character TLDs (e.g. le123@gmail.c)
 * - Maximum 254 characters
 */
export const EMAIL_REGEX = /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$/;

export function isValidEmail(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  const trimmed = value.trim();
  if (!trimmed || trimmed.length > 254) return false;

  // No whitespace inside
  if (/\s/.test(trimmed)) return false;

  // Exactly one '@' separator
  const atParts = trimmed.split('@');
  if (atParts.length !== 2) return false;

  const [localPart, domainPart] = atParts;
  if (!localPart || !domainPart) return false;

  // Local part: valid characters, cannot start or end with a dot, no consecutive dots
  if (!/^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+$/.test(localPart)) return false;
  if (localPart.startsWith('.') || localPart.endsWith('.') || localPart.includes('..')) {
    return false;
  }

  // Domain part: must contain at least one dot
  const domainLabels = domainPart.split('.');
  if (domainLabels.length < 2) return false;

  for (let i = 0; i < domainLabels.length; i++) {
    const label = domainLabels[i];
    // Each label must be non-empty
    if (!label || label.length === 0) return false;

    // Final domain label (TLD): must contain at least 2 alphabetic characters
    if (i === domainLabels.length - 1) {
      if (!/^[a-zA-Z]{2,}$/.test(label)) return false;
    } else {
      // Subdomain / domain label: alphanumeric, optional internal hyphens
      if (!/^[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?$/.test(label)) return false;
    }
  }

  return true;
}

/**
 * Validates Password:
 * - 8..128 characters
 * - At least 1 uppercase letter
 * - At least 1 lowercase letter
 * - At least 1 digit
 */
export function isValidPassword(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  if (value.length < 8 || value.length > 128) return false;
  const hasUpper = /[A-Z]/.test(value);
  const hasLower = /[a-z]/.test(value);
  const hasDigit = /[0-9]/.test(value);
  return hasUpper && hasLower && hasDigit;
}

export type PasswordStrengthLevel = 'weak' | 'medium' | 'strong';

export interface PasswordStrengthResult {
  score: number;
  level: PasswordStrengthLevel;
  hasMinLength: boolean;
  has12Length: boolean;
  hasUpper: boolean;
  hasLower: boolean;
  hasDigit: boolean;
  hasSpecial: boolean;
  isMandatorySatisfied: boolean;
}

/**
 * Deterministic local password-strength scoring:
 * Factors:
 * +1 if length >= 8
 * +1 if length >= 12
 * +1 if contains uppercase
 * +1 if contains lowercase
 * +1 if contains digit
 * +1 if contains special character
 *
 * Mapping:
 * 0..2: Weak
 * 3..4: Medium
 * 5..6: Strong (ONLY if mandatory policy 8..128 + upper + lower + digit is satisfied)
 */
export function calculatePasswordStrength(password: string): PasswordStrengthResult {
  if (!password || typeof password !== 'string') {
    return {
      score: 0,
      level: 'weak',
      hasMinLength: false,
      has12Length: false,
      hasUpper: false,
      hasLower: false,
      hasDigit: false,
      hasSpecial: false,
      isMandatorySatisfied: false,
    };
  }

  const hasMinLength = password.length >= 8;
  const has12Length = password.length >= 12;
  const hasUpper = /[A-Z]/.test(password);
  const hasLower = /[a-z]/.test(password);
  const hasDigit = /[0-9]/.test(password);
  const hasSpecial = /[^A-Za-z0-9\s]/.test(password);

  let score = 0;
  if (hasMinLength) score += 1;
  if (has12Length) score += 1;
  if (hasUpper) score += 1;
  if (hasLower) score += 1;
  if (hasDigit) score += 1;
  if (hasSpecial) score += 1;

  const isMandatorySatisfied =
    hasMinLength && password.length <= 128 && hasUpper && hasLower && hasDigit;

  let level: PasswordStrengthLevel = 'weak';
  if (score >= 5) {
    level = isMandatorySatisfied ? 'strong' : 'medium';
  } else if (score >= 3) {
    level = 'medium';
  } else {
    level = 'weak';
  }

  return {
    score,
    level,
    hasMinLength,
    has12Length,
    hasUpper,
    hasLower,
    hasDigit,
    hasSpecial,
    isMandatorySatisfied,
  };
}

/**
 * Sanitizes bank account number string: digits only, preserves leading zero.
 */
export function sanitizeBankAccountNumber(value: unknown): string {
  return sanitizeDigits(value);
}

/**
 * Validates Vietnam Citizen ID (CCCD) format:
 * Exactly 12 digits, preserved as a digit string with leading zero.
 */
export const VIETNAM_CCCD_REGEX = /^\d{12}$/;

export function isValidVietnamCitizenId(value: unknown): boolean {
  if (!value) return false;
  const digits = sanitizeDigits(value);
  return VIETNAM_CCCD_REGEX.test(digits);
}

/**
 * Returns today's date formatted as local YYYY-MM-DD without UTC timezone drift.
 */
export function getLocalDateString(date: Date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Validates whether a string is a valid calendar date in YYYY-MM-DD format.
 */
export function isValidDateString(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  const trimmed = value.trim();
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed);
  if (!match) return false;

  const year = parseInt(match[1], 10);
  const month = parseInt(match[2], 10);
  const day = parseInt(match[3], 10);

  if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31) {
    return false;
  }

  // Verify real calendar days (e.g. Feb 29/30, April 31)
  const d = new Date(year, month - 1, day);
  return (
    d.getFullYear() === year &&
    d.getMonth() === month - 1 &&
    d.getDate() === day
  );
}

/**
 * Checks if a YYYY-MM-DD date is today or in the future compared to local today.
 */
export function isDateTodayOrFuture(value: unknown, referenceToday?: string): boolean {
  if (!isValidDateString(value)) return false;
  const todayStr = referenceToday || getLocalDateString();
  return String(value).trim() >= todayStr;
}

/**
 * Computes a dynamic future date string in local YYYY-MM-DD (e.g., today + 7 days).
 */
export function getFutureDateString(daysAhead = 7, fromDate: Date = new Date()): string {
  const d = new Date(fromDate.getTime());
  d.setDate(d.getDate() + daysAhead);
  return getLocalDateString(d);
}

