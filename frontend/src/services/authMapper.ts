import type { User, SystemRole, SupportedLocale } from '../types';

export interface BackendUserProfile {
  id: string;
  fullName: string;
  email: string;
  spendId: string;
  phone?: string;
  systemRole: 'User' | 'SystemAdmin';
  status: 'active' | 'restricted' | 'locked' | 'suspended';
  mustChangePassword: boolean;
  preferredLanguage: 'vi-VN' | 'en-US';
  preferredTheme: 'light' | 'dark' | 'system';
  defaultCurrency: 'VND' | 'USD';
  createdAt: string;
  lastLoginAt?: string;
  engagementEmailOptIn: boolean;
}

export function deriveInitials(fullName: string): string {
  if (!fullName) return 'U';
  const parts = fullName.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function mapUserProfile(profile: BackendUserProfile): User {
  return {
    id: profile.id,
    name: profile.fullName,
    email: profile.email,
    role: profile.systemRole as SystemRole,
    language: profile.preferredLanguage as SupportedLocale,
    currency: profile.defaultCurrency,
    avatarInitials: deriveInitials(profile.fullName),
    phone: profile.phone,
    status: profile.status,
    isLocked: profile.status === 'locked' || profile.status === 'suspended',
    mustChangePassword: profile.mustChangePassword,
    createdAt: profile.createdAt,
    engagementEmailOptIn: profile.engagementEmailOptIn ?? false,
  };
}
