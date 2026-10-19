import React, { createContext, useContext, useState, useCallback, ReactNode, useRef } from 'react';
import { SecurityPinChallengeModal } from '../components/security/SecurityPinChallengeModal';

export interface SecurityPinChallengeOptions {
  actionName?: string;
  title?: string;
  description?: string;
  amount?: number;
}

interface SecurityPinChallengeContextType {
  challengePin: (options?: SecurityPinChallengeOptions) => Promise<boolean>;
  challengePinWithValue: (options?: SecurityPinChallengeOptions) => Promise<string | null>;
}

const SecurityPinChallengeContext = createContext<SecurityPinChallengeContextType | undefined>(undefined);

export const SecurityPinChallengeProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [options, setOptions] = useState<SecurityPinChallengeOptions>({});

  const booleanResolverRef = useRef<((result: boolean) => void) | null>(null);
  const valueResolverRef = useRef<((pin: string | null) => void) | null>(null);

  const challengePin = useCallback((opts?: SecurityPinChallengeOptions): Promise<boolean> => {
    return new Promise((resolve) => {
      if (booleanResolverRef.current) booleanResolverRef.current(false);
      if (valueResolverRef.current) valueResolverRef.current(null);
      booleanResolverRef.current = resolve;
      valueResolverRef.current = null;
      setOptions(opts || {});
      setIsOpen(true);
    });
  }, []);

  const challengePinWithValue = useCallback((opts?: SecurityPinChallengeOptions): Promise<string | null> => {
    return new Promise((resolve) => {
      if (booleanResolverRef.current) booleanResolverRef.current(false);
      if (valueResolverRef.current) valueResolverRef.current(null);
      valueResolverRef.current = resolve;
      booleanResolverRef.current = null;
      setOptions(opts || {});
      setIsOpen(true);
    });
  }, []);

  const handleSuccess = useCallback((enteredPin?: string) => {
    setIsOpen(false);
    if (booleanResolverRef.current) {
      booleanResolverRef.current(true);
      booleanResolverRef.current = null;
    }
    if (valueResolverRef.current) {
      valueResolverRef.current(enteredPin || null);
      valueResolverRef.current = null;
    }
  }, []);

  const handleCancel = useCallback(() => {
    setIsOpen(false);
    if (booleanResolverRef.current) {
      booleanResolverRef.current(false);
      booleanResolverRef.current = null;
    }
    if (valueResolverRef.current) {
      valueResolverRef.current(null);
      valueResolverRef.current = null;
    }
  }, []);

  return (
    <SecurityPinChallengeContext.Provider value={{ challengePin, challengePinWithValue }}>
      {children}
      <SecurityPinChallengeModal
        isOpen={isOpen}
        title={options.actionName || options.title}
        description={options.description}
        amount={options.amount}
        onSuccess={handleSuccess}
        onCancel={handleCancel}
      />
    </SecurityPinChallengeContext.Provider>
  );
};

export const useSecurityPinChallenge = (): SecurityPinChallengeContextType => {
  const context = useContext(SecurityPinChallengeContext);
  if (!context) {
    throw new Error('useSecurityPinChallenge must be used within a SecurityPinChallengeProvider');
  }
  return context;
};
