import React, { createContext, useContext, useState, useEffect, ReactNode, useCallback } from 'react';
import { SupportedLocale } from '../types';
import { translations } from './translations';
import { formatMoney } from '../utils/money';

interface I18nContextType {
  locale: SupportedLocale;
  setLocale: (locale: SupportedLocale) => void;
  t: (keyPath: string, params?: Record<string, string | number>) => string;
  formatCurrency: (amount: number) => string;
}

const I18nContext = createContext<I18nContextType | undefined>(undefined);

const STORAGE_KEY = 'spendwise_locale';

export const I18nProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [locale, setLocaleState] = useState<SupportedLocale>(() => {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'vi-VN' || saved === 'en-US') {
      return saved;
    }
    return 'vi-VN';
  });

  const setLocale = useCallback((newLocale: SupportedLocale) => {
    setLocaleState(newLocale);
    localStorage.setItem(STORAGE_KEY, newLocale);
    document.documentElement.lang = newLocale === 'vi-VN' ? 'vi' : 'en';
  }, []);

  useEffect(() => {
    document.documentElement.lang = locale === 'vi-VN' ? 'vi' : 'en';
  }, [locale]);

  const t = useCallback(
    (keyPath: string, params?: Record<string, string | number>): string => {
      const keys = keyPath.split('.');
      let current: any = translations[locale];

      for (const k of keys) {
        if (current && typeof current === 'object' && k in current) {
          current = current[k];
        } else {
          // Fallback to Vietnamese dictionary
          let fallback: any = translations['vi-VN'];
          for (const fbKey of keys) {
            if (fallback && typeof fallback === 'object' && fbKey in fallback) {
              fallback = fallback[fbKey];
            } else {
              fallback = null;
              break;
            }
          }
          current = fallback || keyPath;
          break;
        }
      }

      if (typeof current === 'string') {
        if (params) {
          return Object.entries(params).reduce((str, [paramKey, val]) => {
            return str.replace(new RegExp(`\\{${paramKey}\\}`, 'g'), String(val));
          }, current);
        }
        return current;
      }

      return keyPath;
    },
    [locale]
  );

  const formatCurrency = useCallback(
    (amount: number): string => {
      return formatMoney(amount, locale);
    },
    [locale]
  );

  return (
    <I18nContext.Provider value={{ locale, setLocale, t, formatCurrency }}>
      {children}
    </I18nContext.Provider>
  );
};

export const useI18n = (): I18nContextType => {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error('useI18n must be used within an I18nProvider');
  }
  return context;
};
