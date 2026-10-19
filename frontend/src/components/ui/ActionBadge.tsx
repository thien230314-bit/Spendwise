import React from 'react';
import { cn } from '../../utils/cn';

interface ActionBadgeProps {
  count: number;
  className?: string;
  max?: number;
}

export const ActionBadge: React.FC<ActionBadgeProps> = ({ count, className, max = 99 }) => {
  if (count <= 0) return null;
  const display = count > max ? `${max}+` : String(count);

  return (
    <span
      className={cn(
        'inline-flex items-center justify-center min-w-4.5 h-4.5 px-1 text-[10px] font-bold text-white bg-rose-600 rounded-full leading-none shadow-xs select-none shrink-0 pointer-events-none',
        className
      )}
    >
      {display}
    </span>
  );
};
