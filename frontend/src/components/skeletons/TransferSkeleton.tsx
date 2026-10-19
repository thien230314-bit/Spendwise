import React from 'react';
import { Skeleton } from '../../components/ui/Skeleton';

export const TransferSkeleton: React.FC = () => {
  return (
    <div className="space-y-6 max-w-7xl mx-auto" aria-busy="true">
      {/* Source Wallet Card Skeleton */}
      <div className="rounded-3xl p-5 border border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-between shadow-xs">
        <div className="flex items-center gap-3.5">
          <Skeleton className="h-11 w-11 rounded-2xl shrink-0" />
          <div className="space-y-1.5">
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-5 w-36" />
          </div>
        </div>
        <div className="space-y-1.5 flex flex-col items-end">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-6 w-32" />
        </div>
      </div>

      {/* Main Grid: Form on Left, Conditions on Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Form Area Skeleton */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 border border-slate-100 dark:border-slate-800 shadow-xs space-y-6">
          {/* Phone Lookup Field Skeleton */}
          <div className="space-y-2">
            <div className="flex justify-between items-center">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-28" />
            </div>
            <Skeleton className="h-12 w-full rounded-2xl" />
            {/* Suggested Friends Chips Skeleton */}
            <div className="flex gap-2 pt-1">
              <Skeleton className="h-7 w-24 rounded-xl" />
              <Skeleton className="h-7 w-28 rounded-xl" />
              <Skeleton className="h-7 w-20 rounded-xl" />
            </div>
          </div>

          {/* Amount Field Skeleton */}
          <div className="p-5 rounded-2xl bg-slate-50/80 dark:bg-slate-800/40 border border-slate-200/80 dark:border-slate-700/80 space-y-3">
            <div className="flex justify-between items-center">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-3 w-10" />
            </div>
            <Skeleton className="h-14 w-full rounded-xl" />
            {/* Quick chips */}
            <div className="flex flex-wrap gap-2 pt-1">
              <Skeleton className="h-8 w-20 rounded-xl" />
              <Skeleton className="h-8 w-20 rounded-xl" />
              <Skeleton className="h-8 w-20 rounded-xl" />
              <Skeleton className="h-8 w-24 rounded-xl" />
            </div>
          </div>

          {/* Note Field Skeleton */}
          <div className="space-y-2">
            <Skeleton className="h-4 w-28" />
            <Skeleton className="h-12 w-full rounded-xl" />
          </div>

          {/* Submit Action Button Skeleton */}
          <Skeleton className="h-12 w-full rounded-2xl" />
        </div>

        {/* Right Rail Skeleton */}
        <div className="lg:col-span-5 space-y-6">
          {/* Transfer Conditions Rail Skeleton */}
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-xs space-y-4">
            <Skeleton className="h-5 w-44" />
            <div className="space-y-3">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
              <Skeleton className="h-4 w-4/6" />
            </div>
          </div>

          {/* Pending Reconciliations Card Skeleton */}
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-xs space-y-4">
            <div className="flex justify-between items-center">
              <Skeleton className="h-4 w-36" />
              <Skeleton className="h-5 w-8 rounded-full" />
            </div>
            <div className="space-y-3">
              <Skeleton className="h-14 w-full rounded-2xl" />
              <Skeleton className="h-14 w-full rounded-2xl" />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
