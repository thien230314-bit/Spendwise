import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

interface BalanceVisibilityContextType {
  isHidden: boolean;
  isBalanceHidden: boolean;
  toggleVisibility: () => void;
  setIsHidden: (hidden: boolean) => void;
  maskValue: (value: number | string) => string;
}

const BalanceVisibilityContext = createContext<BalanceVisibilityContextType | undefined>(undefined);
const VISIBILITY_STORAGE_KEY = 'spendwise_hide_balance';

export const BalanceVisibilityProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [isHidden, setIsHiddenState] = useState<boolean>(() => {
    const saved = localStorage.getItem(VISIBILITY_STORAGE_KEY);
    return saved === 'true';
  });

  const toggleVisibility = () => {
    setIsHiddenState(prev => {
      const next = !prev;
      localStorage.setItem(VISIBILITY_STORAGE_KEY, String(next));
      return next;
    });
  };

  const setIsHidden = (hidden: boolean) => {
    setIsHiddenState(hidden);
    localStorage.setItem(VISIBILITY_STORAGE_KEY, String(hidden));
  };

  const maskValue = (value: number | string): string => {
    return '••••••••';
  };

  return (
    <BalanceVisibilityContext.Provider
      value={{
        isHidden,
        isBalanceHidden: isHidden,
        toggleVisibility,
        setIsHidden,
        maskValue,
      }}
    >
      {children}
    </BalanceVisibilityContext.Provider>
  );
};

export const useBalanceVisibility = (): BalanceVisibilityContextType => {
  const context = useContext(BalanceVisibilityContext);
  if (!context) {
    throw new Error('useBalanceVisibility must be used within a BalanceVisibilityProvider');
  }
  return context;
};
