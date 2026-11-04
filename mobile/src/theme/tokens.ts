import { fontStyles } from './fonts';
export { fontStyles } from './fonts';

export type ColorTokens = {
  background: string;
  surface: string;
  surfaceElevated: string;
  primary: string;
  primaryPressed: string;
  accent: string;
  text: string;
  textSecondary: string;
  textMuted: string;
  border: string;
  borderStrong: string;
  danger: string;
  success: string;
  input: string;
  primarySoft: string;
  muted: string;
  button: string;
  onButton: string;
  authBackground: string;
  authBorder: string;
  placeholder: string;
  focus: string;
  surfacePanel: string;
  mint: string;
  emerald: string;
  emeraldDark: string;
  heroGradient: readonly [string, string];
  cardGlass: string;
  amber: string;
  sky: string;
  violet: string;
  cardShadow: string;
};

export const lightColors: ColorTokens = {
  background: '#E6F2EC',
  surface: '#FFFFFF',
  surfaceElevated: '#F5FBF8',
  primary: '#0F9D6E',
  primaryPressed: '#0B7A55',
  accent: '#14C58B',
  text: '#0B2A22',
  textSecondary: '#2F4C42',
  textMuted: '#49675C',
  border: 'rgba(11, 58, 43, 0.16)',
  borderStrong: 'rgba(11, 58, 43, 0.28)',
  danger: '#E5484D',
  success: '#0D8A61',
  input: '#FFFFFF',
  primarySoft: '#B8F0D8',
  muted: 'rgba(11, 42, 34, 0.06)',
  button: '#0F9D6E',
  onButton: '#FFFFFF',
  authBackground: '#E6F2EC',
  authBorder: 'rgba(11, 58, 43, 0.16)',
  placeholder: '#49675C',
  focus: '#0F9D6E',
  surfacePanel: '#FFFFFF',
  mint: '#B8F0D8',
  emerald: '#0F9D6E',
  emeraldDark: '#0B6E50',
  heroGradient: ['#0B6E50', '#12B886'] as const,
  cardGlass: 'rgba(255, 255, 255, 0.88)',
  amber: '#D98218',
  sky: '#2563EB',
  violet: '#7C3AED',
  cardShadow: '0 4px 20px -2px rgba(11, 42, 34, 0.06)',
};

export const darkColors: ColorTokens = {
  background: '#07130F',
  surface: '#122A22',
  surfaceElevated: '#18362C',
  primary: '#2FD39A',
  primaryPressed: '#19B27F',
  accent: '#14C58B',
  text: '#EAF7F2',
  textSecondary: '#B0CBC0',
  textMuted: '#8EAD9F',
  border: 'rgba(255, 255, 255, 0.18)',
  borderStrong: 'rgba(255, 255, 255, 0.30)',
  danger: '#FB7185',
  success: '#2FD39A',
  input: '#122A22',
  primarySoft: '#0F3D2E',
  muted: 'rgba(255, 255, 255, 0.08)',
  button: '#0F9D6E',
  onButton: '#FFFFFF',
  authBackground: '#07130F',
  authBorder: 'rgba(255, 255, 255, 0.18)',
  placeholder: '#8EAD9F',
  focus: '#2FD39A',
  surfacePanel: '#122A22',
  mint: '#0F3D2E',
  emerald: '#2FD39A',
  emeraldDark: '#0A5A42',
  heroGradient: ['#0A5A42', '#0F9D6E'] as const,
  cardGlass: 'rgba(18, 42, 34, 0.86)',
  amber: '#F2A93B',
  sky: '#60A5FA',
  violet: '#A78BFA',
  cardShadow: '0 14px 30px -14px rgba(0, 0, 0, 0.70)',
};

export const spacing = { xs: 4, sm: 8, md: 16, lg: 24, xl: 32, xxl: 48 } as const;
export const radius = { sm: 8, field: 12, md: 16, lg: 20, panel: 24, hero: 28, pill: 999 } as const;

export const typography = {
  brand: { ...fontStyles.bold, fontSize: 18, lineHeight: 24, letterSpacing: -0.4 },
  heroBalance: { ...fontStyles.bold, fontSize: 34, lineHeight: 40, letterSpacing: -0.8 },
  screenTitle: { ...fontStyles.heading, fontSize: 24, lineHeight: 32, letterSpacing: -0.48 },
  authTitle: { ...fontStyles.display, fontSize: 26, lineHeight: 34, letterSpacing: -0.52 },
  sectionTitle: { ...fontStyles.heading, fontSize: 18, lineHeight: 26, letterSpacing: -0.32 },
  body: { ...fontStyles.body, fontSize: 15, lineHeight: 22 },
  caption: { ...fontStyles.body, fontSize: 13, lineHeight: 18 },
  label: { ...fontStyles.semibold, fontSize: 12, lineHeight: 18, letterSpacing: 0.5 },
  control: { ...fontStyles.semibold, fontSize: 15, lineHeight: 22 },
  tabLabel: { ...fontStyles.semibold, fontSize: 11, lineHeight: 15 },
} as const;

export const navigation = { tabHeight: 68, iconSize: 22, iconContainer: 38 } as const;

export const effects = {
  authButtonGradient: 'linear-gradient(90deg, #0b6e50, #0f9d6e, #12b886)',
  authDeskLight: 'linear-gradient(180deg, #ecf6f1 0%, #dfeee7 100%)',
  authDeskDark: 'linear-gradient(180deg, #07130f 0%, #0b1f19 100%)',
  focusRing: '0 0 0 2px rgba(15,157,110,0.3)',
  controlShadow: '0 1px 3px rgba(11,42,34,0.06)',
  cardShadow: '0 4px 20px -2px rgba(11,42,34,0.06), 0 10px 28px -4px rgba(11,42,34,0.08)',
} as const;
