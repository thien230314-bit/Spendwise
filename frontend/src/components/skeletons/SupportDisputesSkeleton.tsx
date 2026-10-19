import React from 'react';
import { Skeleton } from '../../components/ui/Skeleton';

export const SupportDisputesSkeleton: React.FC = () => {
  return (
    <div className="space-y-6 max-w-7xl mx-auto" aria-busy="true">
      {/* Header Skeleton */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="space-y-1.5">
          <Skeleton className="h-7 w-48" />
          <Skeleton className="h-4 w-72" />
        </div>
        <Skeleton className="h-10 w-40 rounded-xl" />
      </div>

      {/* Tabs Skeleton (Yêu cầu hỗ trợ vs Khiếu nại giao dịch) */}
      <div className="flex border-b border-slate-200 dark:border-slate-800 gap-2 pb-2">
        <Skeleton className="h-9 w-40 rounded-lg" />
        <Skeleton className="h-9 w-44 rounded-lg" />
      </div>

      {/* Summary Metrics Row Skeleton */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 space-y-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-7 w-16" />
        </div>
        <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 space-y-2">
          <Skeleton className="h-3 w-28" />
          <Skeleton className="h-7 w-16" />
        </div>
        <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 space-y-2">
          <Skeleton className="h-3 w-28" />
          <Skeleton className="h-7 w-16" />
        </div>
      </div>

      {/* Cases List Skeleton */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 shadow-xs divide-y divide-slate-100 dark:divide-slate-800/80">
        {[1, 2, 3, 4].map((i) => (
          <div key={`case-skel-${i}`} className="p-5 flex items-start justify-between gap-4">
            <div className="flex items-start gap-3.5 flex-1">
              <Skeleton className="h-10 w-10 rounded-2xl shrink-0 mt-0.5" />
              <div className="space-y-2 flex-1">
                <div className="flex items-center gap-2">
                  <Skeleton className="h-4 w-48" />
                  <Skeleton className="h-5 w-20 rounded-full" />
                </div>
                <Skeleton className="h-3.5 w-4/6" />
                <div className="flex items-center gap-3">
                  <Skeleton className="h-3 w-24" />
                  <Skeleton className="h-3 w-28" />
                </div>
              </div>
            </div>
            <Skeleton className="h-8 w-24 rounded-xl" />
          </div>
        ))}
      </div>
    </div>
  );
};
