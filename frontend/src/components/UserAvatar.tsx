import React, { useState, useEffect } from 'react';

export function resolveAvatarUrl(
  avatarUrl?: string | null,
  avatarUpdatedAt?: number
): string | null {
  if (!avatarUrl || !avatarUrl.trim()) return null;
  const trimmed = avatarUrl.trim();
  if (trimmed.startsWith('blob:') || trimmed.startsWith('data:') || /^https?:\/\//i.test(trimmed)) {
    return trimmed;
  }
  if (avatarUpdatedAt) {
    const sep = trimmed.includes('?') ? '&' : '?';
    return `${trimmed}${sep}v=${avatarUpdatedAt}`;
  }
  return trimmed;
}

export function computeInitials(fullName?: string): string {
  if (!fullName || !fullName.trim()) return 'SW';
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0] || ''}${parts[parts.length - 1][0] || ''}`.toUpperCase();
}

export interface UserAvatarProps {
  fullName?: string;
  initials?: string;
  avatarUrl?: string | null;
  avatarUpdatedAt?: number;
  gradientClass?: string;
  gradient?: string;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
  imgClassName?: string;
}

export const UserAvatar: React.FC<UserAvatarProps> = ({
  fullName,
  initials,
  avatarUrl,
  avatarUpdatedAt,
  gradientClass,
  gradient,
  size = 'md',
  className,
  imgClassName = 'h-full w-full object-cover',
}) => {
  const resolvedUrl = resolveAvatarUrl(avatarUrl, avatarUpdatedAt);
  const [imgFailed, setImgFailed] = useState(false);

  useEffect(() => {
    setImgFailed(false);
  }, [resolvedUrl]);

  const fallbackText = initials || computeInitials(fullName);
  const effectiveGradient = gradientClass || gradient || 'from-emerald-600 to-teal-800';
  const sizeClass = size === 'sm' ? 'sw-av sm' : size === 'lg' ? 'sw-av lg' : 'sw-av';
  const wrapperClass = className ? `${sizeClass} ${className}` : sizeClass;

  if (resolvedUrl && !imgFailed) {
    return (
      <div className={`relative overflow-hidden ${wrapperClass}`}>
        <img
          src={resolvedUrl}
          alt={fullName ? `Ảnh đại diện của ${fullName}` : 'Ảnh đại diện'}
          onError={() => setImgFailed(true)}
          className={imgClassName}
        />
      </div>
    );
  }

  return (
    <div
      className={`flex items-center justify-center font-display font-bold text-white bg-gradient-to-br ${effectiveGradient} select-none ${wrapperClass}`}
      aria-label={fullName || 'Người dùng'}
    >
      <span className="leading-none">{fallbackText}</span>
    </div>
  );
};
