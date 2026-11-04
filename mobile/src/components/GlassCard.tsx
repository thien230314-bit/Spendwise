import React, { PropsWithChildren } from 'react';
import { View, StyleSheet, ViewStyle, StyleProp, Platform } from 'react-native';
import { BlurView } from 'expo-blur';
import { useTheme } from '@/theme/useTheme';
import { radius } from '@/theme/tokens';

export interface GlassCardProps extends PropsWithChildren {
  style?: StyleProp<ViewStyle>;
  intensity?: number;
  elevated?: boolean;
}

export function GlassCard({ children, style, intensity = 25, elevated = false }: GlassCardProps) {
  const { colors, isDark } = useTheme();

  return (
    <View
      style={[
        styles.container,
        {
          backgroundColor: elevated ? colors.surfaceElevated : colors.cardGlass,
          borderColor: colors.border,
        },
        style,
      ]}
    >
      {Platform.OS === 'ios' && (
        <BlurView
          intensity={intensity}
          tint={isDark ? 'dark' : 'light'}
          style={StyleSheet.absoluteFill}
        />
      )}
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: radius.panel,
    borderWidth: 1,
    overflow: 'hidden',
    shadowColor: '#0b2a22',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.06,
    shadowRadius: 12,
    elevation: 3,
  },
});
