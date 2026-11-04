import React, { createContext, useContext, ReactNode } from 'react';
import { useI18n } from '../i18n/I18nContext';
import { useTheme } from '../providers/ThemeProvider';

export function formatVND(amount: number): string {
  if (!Number.isFinite(amount)) return '0 ₫';
  return `${amount.toLocaleString('vi-VN')} ₫`;
}

export interface SpendWiseContextType {
  tr: (vi: string, en?: string) => string;
  language: 'vi' | 'en';
  setLanguage: (lang: 'vi' | 'en') => void;
  resolvedTheme: 'light' | 'dark';
  setTheme: (theme: 'light' | 'dark' | 'system') => void;
  formatVND: (amount: number) => string;
}

const SpendWiseContext = createContext<SpendWiseContextType | undefined>(undefined);

export const SpendWiseProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { locale, setLocale } = useI18n();
  const { setTheme, resolvedTheme } = useTheme();

  const language: 'vi' | 'en' = locale.startsWith('en') ? 'en' : 'vi';

  const setLanguage = (lang: 'vi' | 'en') => {
    setLocale(lang === 'en' ? 'en-US' : 'vi-VN');
  };

  const tr = (vi: string, en?: string): string => {
    if (language === 'en' && en) return en;
    return vi;
  };

  return (
    <SpendWiseContext.Provider
      value={{
        tr,
        language,
        setLanguage,
        resolvedTheme,
        setTheme,
        formatVND,
      }}
    >
      {children}
    </SpendWiseContext.Provider>
  );
};

export function useSpendWise(): SpendWiseContextType {
  const ctx = useContext(SpendWiseContext);
  if (!ctx) {
    return {
      tr: (vi: string, en?: string) =>
        typeof window !== 'undefined' && document.documentElement.lang === 'en' && en ? en : vi,
      language: 'vi',
      setLanguage: () => {},
      resolvedTheme: 'light',
      setTheme: () => {},
      formatVND,
    };
  }
  return ctx;
}
