import { StyleSheet, Text, View } from 'react-native';
import { BrandLogo } from './BrandLogo';
import { spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

type Props = { title: string; subtitle?: string };

export function AppHeader({ title, subtitle }: Props) {
  const { colors } = useTheme();
  return (
    <View style={styles.header}>
      <BrandLogo compact />
      <Text accessibilityRole="header" style={[typography.screenTitle, { color: colors.text }]}>{title}</Text>
      {subtitle ? <Text style={[typography.body, { color: colors.textMuted }]}>{subtitle}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({ header: { gap: spacing.sm } });
