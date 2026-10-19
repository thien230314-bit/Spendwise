import React from 'react';
import { Skeleton } from '../../components/ui/Skeleton';

export interface FriendsSkeletonProps {
  /** If true, renders a realistic empty state placeholder instead of populated cards */
  emptyState?: boolean;
}

export const FriendsSkeleton: React.FC<FriendsSkeletonProps> = ({ emptyState = false }) => {
  return (
    <div className="w-full space-y-6 max-w-7xl mx-auto overflow-hidden" aria-busy="true">
      {/* 1. Header & Action "Kết bạn mới" */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 dark:border-slate-800 pb-5">
        <div className="flex items-center gap-2.5">
          <Skeleton className="h-10 w-10 rounded-xl shrink-0" />
          <div className="space-y-1.5">
            <Skeleton className="h-6 w-44 rounded-md" />
            <Skeleton className="h-3.5 w-72 max-w-full rounded-md" />
          </div>
        </div>
        <Skeleton className="h-10 w-32 rounded-xl shrink-0 self-start sm:self-center" />
      </div>

      {/* 2. Three Tabs: Bạn bè, Lời mời nhận được, Đã gửi */}
      <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2 overflow-x-auto no-scrollbar">
        <Skeleton className="h-9 w-28 rounded-lg shrink-0" />
        <Skeleton className="h-9 w-36 rounded-lg shrink-0" />
        <Skeleton className="h-9 w-28 rounded-lg shrink-0" />
      </div>

      {/* 3. Split Controls: Local Friend Filter & Remote User Lookup */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Left: Filter Current Friends Box */}
        <div className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/60 dark:bg-slate-900/40 space-y-2.5">
          <div className="flex items-center justify-between">
            <Skeleton className="h-3.5 w-36 rounded-sm" />
            <Skeleton className="h-3 w-16 rounded-sm" />
          </div>
          <Skeleton className="h-9 w-full rounded-xl" />
        </div>

        {/* Right: Remote User Lookup Box */}
        <div className="p-4 rounded-2xl border border-emerald-200/80 dark:border-emerald-800/40 bg-emerald-50/20 dark:bg-emerald-950/10 space-y-2.5">
          <div className="flex items-center justify-between">
            <Skeleton className="h-3.5 w-32 rounded-sm" />
            <Skeleton className="h-3 w-28 rounded-sm" />
          </div>
          <div className="flex items-center gap-2">
            <Skeleton className="h-9 flex-1 rounded-xl" />
            <Skeleton className="h-9 w-20 rounded-xl shrink-0" />
          </div>
        </div>
      </div>

      {/* 4. Results: Empty State vs Cards Grid */}
      {emptyState ? (
        <div className="p-8 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs text-center max-w-xl mx-auto my-6 flex flex-col items-center">
          <Skeleton className="w-12 h-12 rounded-xl mb-3" />
          <Skeleton className="h-4 w-48 rounded-md mb-2" />
          <Skeleton className="h-3 w-72 max-w-full rounded-sm" />
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((idx) => (
            <div
              key={`friend-card-skel-${idx}`}
              className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs flex flex-col justify-between space-y-4 overflow-hidden min-w-0"
            >
              {/* Card Top: Avatar, Name, SpendId, Star */}
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-3 min-w-0">
                  <Skeleton className="w-12 h-12 rounded-full shrink-0" />
                  <div className="space-y-1.5 min-w-0">
                    <Skeleton className="h-4 w-28 rounded-sm" />
                    <Skeleton className="h-3 w-20 rounded-sm" />
                    <Skeleton className="h-3 w-24 rounded-sm" />
                  </div>
                </div>
                <Skeleton className="w-7 h-7 rounded-lg shrink-0" />
              </div>

              {/* Card Bottom: Action buttons */}
              <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5">
                  <Skeleton className="w-8 h-8 rounded-xl" />
                  <Skeleton className="w-8 h-8 rounded-xl" />
                </div>
                <Skeleton className="h-8 w-24 rounded-xl" />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
