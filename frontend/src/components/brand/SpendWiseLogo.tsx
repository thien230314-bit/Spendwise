import React from 'react';
import { cn } from '../../utils/cn';

export interface SpendWiseLogoProps {
  /**
   * Kích thước của Logo:
   * - Preset: 'xs' (16px), 'sm' (20px), 'md' (24px), 'lg' (32px), 'xl' (40px), '2xl' (48px), '3xl' (64px), 'hero' (96px)
   * - Number: giá trị pixel tùy chọn (ví dụ: 36, 56, 80)
   */
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | '2xl' | '3xl' | 'hero' | number;
  /**
   * Kiểu hiển thị được giữ lại để tương thích với các vị trí đang sử dụng component.
   */
  variant?: 'badge' | 'icon-only';
  /**
   * Tuỳ chọn màu sắc được giữ lại để tương thích với API hiện có.
   */
  color?: string;
  /**
   * Tuỳ chọn ưu tiên ảnh được giữ lại để tương thích với API hiện có.
   */
  preferImageFile?: boolean;
  className?: string;
  id?: string;
}

const SIZE_MAP: Record<string, number> = {
  xs: 16,
  sm: 20,
  md: 24,
  lg: 32,
  xl: 40,
  '2xl': 48,
  '3xl': 64,
  hero: 96,
};

export const SpendWiseLogo: React.FC<SpendWiseLogoProps> = ({
  size = 'md',
  className,
  id,
}) => {
  const pixelSize = typeof size === 'number' ? size : SIZE_MAP[size] || 32;
  const borderRadius = Math.max(2, Math.round(pixelSize * 0.11));

  return (
    <img
      id={id}
      src="/spenwise-logo.png"
      alt="SpendWise Logo"
      width={pixelSize}
      height={pixelSize}
      style={{ borderRadius: `${borderRadius}px` }}
      className={cn(
        'inline-block shrink-0 select-none object-contain overflow-hidden transition-transform duration-200',
        className
      )}
    />
  );
};
