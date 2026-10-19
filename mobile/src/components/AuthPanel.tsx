import type { PropsWithChildren } from 'react';
import { AuthNotebookShell } from './AuthNotebookShell';

/**
 * Backward compatibility wrapper delegating to AuthNotebookShell.
 */
export function AuthPanel({ children, compact = false }: PropsWithChildren<{ compact?: boolean }>) {
  return (
    <AuthNotebookShell
      footerNote={compact ? 'Bảo mật tài khoản SpendWise' : 'Bảo mật tài chính cá nhân'}
    >
      {children}
    </AuthNotebookShell>
  );
}
