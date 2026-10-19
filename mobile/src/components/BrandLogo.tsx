import { Image, StyleSheet, Text, View } from 'react-native';
import { fontStyles } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function BrandLogo({ compact = false }: { compact?: boolean }) {
  const { colors } = useTheme();
  const size = 38;
  return (
    <View style={styles.row}>
      <Image source={require('../../assets/spenwise-logo.png')} resizeMode="contain" style={{ width: size, height: size }} accessibilityLabel="SpendWise logo" />
      <View>
        <Text style={[styles.title, { color: colors.text }]}>Spend<Text style={[fontStyles.wordmarkStrong, { color: colors.primary }]}>Wise</Text></Text>
        <Text style={[styles.subtitle, { color: colors.textMuted }]}>{compact ? 'FINANCE OS' : 'FINANCE PLATFORM'}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  title: { ...fontStyles.bold, fontSize: 16, lineHeight: 22, letterSpacing: -0.4 },
  subtitle: { ...fontStyles.bold, fontSize: 10, lineHeight: 16, letterSpacing: 0.9 },
});
