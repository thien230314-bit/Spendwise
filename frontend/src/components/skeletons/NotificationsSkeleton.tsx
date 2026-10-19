import React from 'react';
import { Skeleton } from '../../components/ui/Skeleton';

export const NotificationsSkeleton: React.FC = () => {
  return (
    <div className="space-y-6 max-w-5xl mx-auto" aria-busy="true">
      {/* Header Skeleton */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="space-y-1.5">
          <Skeleton className="h-7 w-44" />
          <Skeleton className="h-4 w-64" />
        </div>
        <div className="flex items-center gap-2">
          <Skeleton className="h-9 w-28 rounded-xl" />
          <Skeleton className="h-9 w-28 rounded-xl" />
        </div>
      </div>

      {/* Filter Tabs Skeleton */}
      <div className="flex gap-2 overflow-x-auto pb-2">
        <Skeleton className="h-8 w-20 rounded-full" />
        <Skeleton className="h-8 w-28 rounded-full" />
        <Skeleton className="h-8 w-24 rounded-full" />
        <Skeleton className="h-8 w-28 rounded-full" />
        <Skeleton className="h-8 w-24 rounded-full" />
      </div>

      {/* Notifications List Skeleton */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 shadow-xs divide-y divide-slate-100 dark:divide-slate-800/80">
        {[1, 2, 3, 4, 5].map((i) => (
          <div key={`notif-skel-${i}`} className="p-4 sm:p-5 flex items-start gap-3.5">
            <Skeleton className="h-10 w-10 rounded-2xl shrink-0 mt-0.5" />
            <div className="flex-1 space-y-2">
              <div className="flex justify-between items-start gap-2">
                <Skeleton className="h-4 w-48" />
                <Skeleton className="h-3 w-16" />
              </div>
              <Skeleton className="h-3.5 w-5/6" />
              <div className="flex items-center gap-2 pt-1">
                <Skeleton className="h-5 w-20 rounded-full" />
                <Skeleton className="h-5 w-24 rounded-full" />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
