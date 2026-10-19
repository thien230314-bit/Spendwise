import React from 'react';
import { cn } from '../../utils/cn';

export interface SpinnerProps extends React.SVGAttributes<SVGSVGElement> {
  size?: 'xs' | 'sm' | 'md' | 'lg';
  variant?: 'white' | 'primary' | 'muted';
}

const sizeClasses = {
  xs: 'h-3.5 w-3.5',
  sm: 'h-4 w-4',
  md: 'h-5 w-5',
  lg: 'h-6 w-6',
};

const variantClasses = {
  white: 'text-white',
  primary: 'text-emerald-600 dark:text-emerald-400',
  muted: 'text-slate-400 dark:text-slate-500',
};

export const Spinner: React.FC<SpinnerProps> = ({
  size = 'sm',
  variant = 'white',
  className,
  ...props
}) => {
  return (
    <svg
      className={cn('animate-spin shrink-0', sizeClasses[size], variantClasses[variant], className)}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
      aria-hidden="true"
      {...props}
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="3.5"
      />
      <path
        className="opacity-90"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
};
