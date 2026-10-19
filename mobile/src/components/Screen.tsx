import type { PropsWithChildren } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import type { ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { authEffects } from '@/theme/authTokens';
import { useTheme } from '@/theme/useTheme';

export function Screen({ children }: PropsWithChildren) {
  const { colors, isDark } = useTheme();
  const gradient = isDark ? authEffects.darkDesk : authEffects.lightDesk;
  const deskStyle: ViewStyle = Platform.OS === 'web' ? { backgroundImage: gradient } as ViewStyle : {};
  return (
    <SafeAreaView style={[styles.safe, { backgroundColor: colors.authBackground }, deskStyle]}>
      <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : 'height'} enabled={Platform.OS !== 'web'}>
      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        <View style={styles.content}>{children}</View>
      </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1 },
  scroll: { flexGrow: 1, paddingHorizontal: 12, paddingTop: 28, paddingBottom: 28 },
  content: { alignSelf: 'center', maxWidth: 560, width: '100%' },
});
