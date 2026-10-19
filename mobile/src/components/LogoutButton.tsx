import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { useAuth } from '@/auth/AuthProvider';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { errorMessage } from '@/utils/errorMessage';

export function LogoutButton() {
  const { colors } = useTheme();
  const { logout } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const signOut = async () => {
    setBusy(true); setError('');
    try { await logout(); } catch (reason) { setError(errorMessage(reason)); } finally { setBusy(false); }
  };
  return <>{error ? <Text accessibilityRole="alert" style={{ ...fontStyles.medium, color: colors.danger }}>{error}</Text> : null}<Pressable accessibilityRole="button" accessibilityLabel="Đăng xuất" accessibilityState={{ disabled: busy, busy }} disabled={busy} onPress={signOut} style={({ pressed }) => [styles.button, { borderColor: colors.danger, opacity: pressed || busy ? 0.65 : 1 }]}>{busy ? <Text style={[styles.label, { color: colors.danger }]}>Đang đăng xuất...</Text> : <Text style={[styles.label, { color: colors.danger }]}>Đăng xuất</Text>}</Pressable></>;
}
const styles = StyleSheet.create({ button: { borderWidth: 1, borderRadius: radius.md, minHeight: 52, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.md, paddingVertical: spacing.sm }, label: { fontSize: 16, lineHeight: 24, ...fontStyles.bold } });
