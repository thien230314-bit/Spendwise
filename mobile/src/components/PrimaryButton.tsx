import { Pressable, StyleSheet, Text, Platform } from 'react-native';
import type { ViewStyle } from 'react-native';
import { effects, fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

type Props = { label: string; onPress: () => void; disabled?: boolean; loading?: boolean; auth?: boolean };

export function PrimaryButton({ label, onPress, disabled, loading, auth = false }: Props) {
  const { colors } = useTheme();
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled: !!(disabled || loading), busy: !!loading }} disabled={disabled || loading} onPress={onPress}
      style={({ pressed }) => [styles.button, { backgroundColor: disabled ? colors.muted : pressed ? colors.primaryPressed : colors.button, borderColor: disabled ? colors.border : 'rgba(52,211,153,0.2)' }, auth && !disabled && !pressed && styles.auth]}>
      <Text style={[styles.label, { color: disabled ? colors.textMuted : colors.onButton }]}>
        {loading ? 'Đang xử lý...' : label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: { alignItems: 'center', borderRadius: radius.field, borderWidth: 1, justifyContent: 'center', minHeight: 48, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  // Web's emerald gradient language; darker centre keeps white control text legible.
  auth: { ...Platform.select({ web: { backgroundImage: effects.authButtonGradient } as ViewStyle, default: {} }), boxShadow: '0 6px 14px rgba(6,78,59,0.25)' },
  label: { ...fontStyles.bold, fontSize: 14, lineHeight: 22, textAlign: 'center' },
});
