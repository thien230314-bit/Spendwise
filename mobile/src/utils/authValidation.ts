// Mirrors locked Web inputValidation.ts. Server remains authoritative.
export const validPassword = (value: string) => value.length >= 8 && value.length <= 128 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /[0-9]/.test(value);
export const validPhone = (value: string) => /^0\d{9}$/.test(value);
export const validName = (value: string) => value.trim().length >= 2 && value.trim().length <= 100 && /\p{L}/u.test(value) && /^[\p{L}\s'’.-]+$/u.test(value);
export function validEmail(value: string) {
  const clean = value.trim();
  if (!clean || clean.length > 254 || /\s/.test(clean)) return false;
  const parts = clean.split('@');
  if (parts.length !== 2) return false;
  const [local, domain] = parts;
  if (!local || !/^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+$/.test(local) || local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false;
  const labels = domain.split('.');
  return labels.length >= 2 && labels.every((label, i) => i === labels.length - 1 ? /^[a-zA-Z]{2,}$/.test(label) : /^[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?$/.test(label));
}
export const passwordGuidance = 'Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và số.';
