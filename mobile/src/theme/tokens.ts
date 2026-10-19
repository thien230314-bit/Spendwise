import { fontStyles } from './fonts';
export { fontStyles } from './fonts';

export type ColorTokens = {
  background: string; surface: string; surfaceElevated: string; primary: string;
  primaryPressed: string; accent: string; text: string; textMuted: string;
  border: string; danger: string; success: string; input: string;
  primarySoft: string; muted: string; button: string; onButton: string;
  authBackground: string; authBorder: string; placeholder: string; focus: string; surfacePanel: string;
};

export const lightColors: ColorTokens = {
  background: '#F8FAFC', surface: '#FCFBF7', surfaceElevated: '#FFFFFF',
  primary: '#047857', primaryPressed: '#065F46', accent: '#06B6D4',
  text: '#0F172A', textMuted: '#64748B', border: '#E2E8F0',
  danger: '#DC2626', success: '#15803D', input: '#FFFFFF',
  primarySoft: '#ECFDF5', muted: '#F1F5F9', button: '#047857', onButton: '#FFFFFF',
  authBackground: '#D7C1A2', authBorder: '#DCD4C0', placeholder: '#64748B', focus: '#10B981',
  surfacePanel: '#FFFFFF',
};

export const darkColors: ColorTokens = {
  background: '#090D16', surface: '#101622', surfaceElevated: '#0F1422',
  primary: '#34D399', primaryPressed: '#065F46', accent: '#22D3EE',
  text: '#F1F5F9', textMuted: '#94A3B8', border: '#1E293B',
  danger: '#F87171', success: '#4ADE80', input: '#151B22',
  primarySoft: '#052E26', muted: '#1E293B', button: '#047857', onButton: '#FFFFFF',
  authBackground: '#140F0C', authBorder: '#182230', placeholder: '#94A3B8', focus: '#10B981',
  surfacePanel: '#0F172A',
};

export const spacing = { xs: 4, sm: 8, md: 16, lg: 24, xl: 32, xxl: 48 } as const;
export const radius = { sm: 8, field: 10, md: 12, lg: 16, panel: 24, pill: 999 } as const;

export const typography = {
  brand: { ...fontStyles.bold, fontSize: 18, lineHeight: 24, letterSpacing: -0.4 },
  screenTitle: { ...fontStyles.heading, fontSize: 24, lineHeight: 32, letterSpacing: -0.48 },
  authTitle: { ...fontStyles.display, fontSize: 26, lineHeight: 34, letterSpacing: -0.52 },
  sectionTitle: { ...fontStyles.heading, fontSize: 16, lineHeight: 24, letterSpacing: -0.32 },
  body: { ...fontStyles.body, fontSize: 14, lineHeight: 22 },
  caption: { ...fontStyles.body, fontSize: 12, lineHeight: 18 },
  label: { ...fontStyles.semibold, fontSize: 11, lineHeight: 18, letterSpacing: 0.65, textTransform: 'uppercase' },
  control: { ...fontStyles.semibold, fontSize: 14, lineHeight: 22 },
  tabLabel: { ...fontStyles.semibold, fontSize: 11, lineHeight: 16 },
} as const;

export const navigation = { tabHeight: 76, iconSize: 26, iconContainer: 40 } as const;

export const effects = {
  // Web auth gradient language, with accessible white-text contrast at the centre.
  authButtonGradient: 'linear-gradient(90deg, #059669, #047857, #059669)',
  authDeskLight: 'linear-gradient(120deg, #DFCBAF, #D7C1A2, #E2CEB4)',
  authDeskDark: 'linear-gradient(120deg, #1B140F, #140F0C, #18120E)',
  focusRing: '0 0 0 2px rgba(16,185,129,0.2)',
  controlShadow: '0 1px 2px rgba(15,23,42,0.03)',
  cardShadow: '0 1px 2px rgba(15,23,42,0.04)',
} as const;
