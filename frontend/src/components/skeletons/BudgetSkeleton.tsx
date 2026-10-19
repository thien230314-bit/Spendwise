import React from 'react';
import { Skeleton } from '../../components/ui/Skeleton';

export const BudgetSkeleton: React.FC = () => {
  return (
    <div className="space-y-6 max-w-7xl mx-auto" aria-busy="true">
      {/* Header & Controls Skeleton */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="space-y-1.5">
          <Skeleton className="h-7 w-48" />
          <Skeleton className="h-4 w-72" />
        </div>
        <div className="flex items-center gap-3">
          <Skeleton className="h-10 w-36 rounded-2xl" />
          <Skeleton className="h-10 w-36 rounded-xl" />
        </div>
      </div>

      {/* Budget Overview Card Skeleton (Spending Limit First) */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 p-6 shadow-sm">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-center">
          {/* Left Column: Dominant Remaining & Equation */}
          <div className="lg:col-span-7 space-y-3">
            <div className="space-y-1.5">
              <Skeleton className="h-3 w-40" />
              <div className="flex items-baseline gap-3">
                <Skeleton className="h-10 w-52" />
                <Skeleton className="h-5 w-28 rounded-full" />
              </div>
            </div>
            {/* Financial equation chip */}
            <Skeleton className="h-9 w-96 max-w-full rounded-2xl" />
          </div>

          {/* Right Column: Progress bar */}
          <div className="lg:col-span-5 space-y-2">
            <div className="flex justify-between items-center">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-4 w-12" />
            </div>
            <Skeleton className="h-4 w-full rounded-full" />
            <Skeleton className="h-3 w-44" />
          </div>
        </div>
      </div>

      {/* Budget Category Cards Grid Skeleton */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <div
            key={`b-skel-${i}`}
            className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 p-5 shadow-xs space-y-4"
          >
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <Skeleton className="h-10 w-10 rounded-2xl shrink-0" />
                <div className="space-y-1">
                  <Skeleton className="h-4 w-28" />
                  <Skeleton className="h-3 w-16" />
                </div>
              </div>
              <Skeleton className="h-6 w-16 rounded-full" />
            </div>

            <div className="space-y-2">
              <div className="flex justify-between">
                <Skeleton className="h-3.5 w-20" />
                <Skeleton className="h-3.5 w-24" />
              </div>
              <Skeleton className="h-2.5 w-full rounded-full" />
              <div className="flex justify-between">
                <Skeleton className="h-3 w-16" />
                <Skeleton className="h-3 w-20" />
              </div>
            </div>

            <div className="pt-3 border-t border-slate-100 dark:border-slate-800/80 flex justify-between items-center">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-6 w-16 rounded-lg" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
