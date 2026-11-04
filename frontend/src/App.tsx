import React from 'react';
import { I18nProvider } from './i18n/I18nContext';
import { ThemeProvider } from './providers/ThemeProvider';
import { AuthProvider } from './providers/AuthProvider';
import { SecurityPinChallengeProvider } from './providers/SecurityPinChallengeProvider';
import { SpendWiseProvider } from './context/SpendWiseContext';
import { AppRouter } from './app/router/AppRouter';

export default function App() {
  return (
    <ThemeProvider>
      <I18nProvider>
        <AuthProvider>
          <SecurityPinChallengeProvider>
            <SpendWiseProvider>
              <AppRouter />
            </SpendWiseProvider>
          </SecurityPinChallengeProvider>
        </AuthProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}
