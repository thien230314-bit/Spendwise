import type { PropsWithChildren } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { authType } from '@/theme/authTokens';
import { useAuthColors } from './AuthUI';

export function AuthFormSurface({ children, title, subtitle, footerNote = 'Bảo mật tài chính cá nhân' }: PropsWithChildren<{ title?: string; subtitle?: string; footerNote?: string }>) {
  const c = useAuthColors();
  return <ScrollView showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag" contentContainerStyle={styles.content}>
    {title && <AuthFormHeading title={title} subtitle={subtitle ?? ''} />}
    {children}
    <View style={[styles.footer, { borderTopColor: c.border }]}>
      <SymbolView name={{ ios: 'lock.fill', android: 'lock', web: 'lock' }} size={14} tintColor={c.muted} />
      <Text style={[authType.subtitle, styles.footerText, { color: c.muted }]}>{footerNote}</Text>
    </View>
  </ScrollView>;
}
// Scoped to the paired forms; PIN and recovery typography is unchanged.
export function AuthFormHeading({ title, subtitle }: { title: string; subtitle: string }) {
  const c = useAuthColors();
  return <View style={styles.heading}>
    <Text accessibilityRole="header" style={[authType.title, styles.title, { color: c.text }]}>{title}</Text>
    <Text style={[authType.subtitle, styles.subtitle, { color: c.muted }]}>{subtitle}</Text>
  </View>;
}
const styles = StyleSheet.create({
  content: { flexGrow: 1, paddingHorizontal: 20, paddingVertical: 16, gap: 12 },
  heading: { gap: 4 },
  title: { fontSize: 26, lineHeight: 34, textAlign: 'center' },
  subtitle: { fontSize: 12, lineHeight: 20, textAlign: 'center' },
  footerText: { fontSize: 12, lineHeight: 20 },
  footer: { borderTopWidth: 1, paddingTop: 12, marginTop: 4, flexDirection: 'row', flexWrap: 'wrap', gap: 8, alignItems: 'center' },
});
