import React from 'react';
import { cn } from '../../../utils/cn';
import { NotebookAuth } from '../../auth/NotebookAuth';

interface MobileAuthScreenProps {
  theme?: 'light' | 'dark';
  onLoginSuccess?: () => void;
  statusMode?: 'normal' | 'loading' | 'error' | 'success';
}

export const MobileAuthScreen: React.FC<MobileAuthScreenProps> = ({
  theme = 'light',
  onLoginSuccess,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';

  return (
    <div
      className={cn(
        'flex-1 flex flex-col justify-start select-none transition-colors relative overflow-y-auto',
        isDark ? 'bg-[#090D16]' : 'bg-[#edf2f9]'
      )}
    >
      <NotebookAuth
        onLoginSuccess={onLoginSuccess}
        statusMode={statusMode}
      />
    </div>
  );
};
