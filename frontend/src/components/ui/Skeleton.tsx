import React, { HTMLAttributes } from 'react';
import { cn } from '../../utils/cn';

export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  rounded?: 'sm' | 'md' | 'lg' | 'full';
  shimmer?: boolean;
}

export const Skeleton: React.FC<SkeletonProps> = ({
  className,
  rounded = 'md',
  shimmer = true,
  ...props
}) => {
  const roundedClasses = {
    sm: 'rounded-sm',
    md: 'rounded-md',
    lg: 'rounded-xl',
    full: 'rounded-full',
  };

  return (
    <div
      aria-hidden="true"
      className={cn(
        'bg-slate-200/80 dark:bg-slate-800/80',
        shimmer ? 'animate-shimmer' : 'animate-pulse',
        roundedClasses[rounded],
        className
      )}
      {...props}
    />
  );
};
