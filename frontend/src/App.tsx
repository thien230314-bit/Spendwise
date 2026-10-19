import React from 'react';
import { I18nProvider } from './i18n/I18nContext';
import { ThemeProvider } from './providers/ThemeProvider';
import { AuthProvider } from './providers/AuthProvider';
import { BalanceVisibilityProvider } from './providers/BalanceVisibilityProvider';
import { FinancialProvider } from './providers/FinancialProvider';
import { GroupProvider } from './providers/GroupProvider';
import { NotificationProvider } from './providers/NotificationProvider';
import { SecurityPinChallengeProvider } from './providers/SecurityPinChallengeProvider';
import { AppRouter } from './app/router/AppRouter';

export default function App() {
  return (
    <ThemeProvider>
      <I18nProvider>
        <AuthProvider>
          <SecurityPinChallengeProvider>
            <BalanceVisibilityProvider>
              <FinancialProvider>
                <GroupProvider>
                  <NotificationProvider>
                    <AppRouter />
                  </NotificationProvider>
                </GroupProvider>
              </FinancialProvider>
            </BalanceVisibilityProvider>
          </SecurityPinChallengeProvider>
        </AuthProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}

