import type { PropsWithChildren, ReactNode } from 'react';
import { Image, KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import type { ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { SymbolView } from 'expo-symbols';
import { fontStyles } from '@/theme/fonts';
import { authEffects, authNotebookDark, authNotebookLight } from '@/theme/authTokens';
import { useAuthColors } from './auth/AuthUI';
import { useTheme } from '@/theme/useTheme';

export interface AuthNotebookShellProps extends PropsWithChildren {
  header?: ReactNode;
  title?: string;
  subtitle?: string;
  badge?: ReactNode;
  footerNote?: string;
}

// One scrolling viewport, with no nested form scroller or visible scroll track.
export function AuthNotebookScreen({ children }: PropsWithChildren) {
  const { colors, isDark } = useTheme();
  const desk: ViewStyle = Platform.OS === 'web'
    ? { backgroundImage: isDark ? authEffects.darkDesk : authEffects.lightDesk } as ViewStyle : {};
  return <SafeAreaView style={[styles.safe, { backgroundColor: colors.authBackground }, desk]}>
    <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : 'height'} enabled={Platform.OS !== 'web'}>
      <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        <View style={styles.content}>{children}</View>
      </ScrollView>
    </KeyboardAvoidingView>
  </SafeAreaView>;
}

// Static shell retained for recovery/PIN routes, independent of Login/Register motion.
// The paper remains a separate surface from the cover.
export function AuthNotebookShell({
  children, header, title, subtitle, badge,
  footerNote = 'Bảo mật tài chính cá nhân',
}: AuthNotebookShellProps) {
  const c = useAuthColors();
  const { isDark } = useTheme();
  const book = isDark ? authNotebookDark : authNotebookLight;
  const webImage = (backgroundImage: string): ViewStyle =>
    Platform.OS === 'web' ? { backgroundImage } as ViewStyle : {};

  const front = <View testID="auth-notebook-form-page" style={[styles.page, { backgroundColor: c.paper, boxShadow: `3px 2px 0 ${c.paperEdge3}, 6px 4px 0 ${c.paperEdge2}, 0 8px 18px rgba(0,0,0,0.14)` }]}>
      <View aria-hidden accessible={false} style={[styles.pageCurve, webImage(book.pageCurve.replace('to bottom', 'to right'))]} />
      <View testID="auth-notebook-binding" aria-hidden accessible={false} style={styles.binding}>
        {Array.from({ length: 8 }, (_, index) => <View key={index} style={[styles.ringUnit, { top: `${7 + index * 11.8}%` }]}>
          <View style={[styles.hole, { backgroundColor: book.hole }]} />
          <View style={[styles.ring, { backgroundColor: book.ringMetal }, webImage(book.ringGradient)]} />
          <View style={[styles.hole, { backgroundColor: book.hole }]} />
        </View>)}
      </View>

      {header !== undefined ? header : <View style={[styles.brandHeader, { borderBottomColor: c.border }]}>
        <Image source={require('../../assets/spenwise-logo.png')} resizeMode="contain" style={styles.logo} accessibilityLabel="SpendWise logo" />
        <View style={styles.brandCopy}>
          <Text style={[styles.wordmark, { color: c.text }]}>Spend<Text style={[fontStyles.wordmarkStrong, { color: c.link }]}>Wise</Text></Text>
          <Text style={[styles.brandCaption, { color: c.muted }]}>FINANCE PLATFORM</Text>
        </View>
      </View>}
      {(title || subtitle || badge) && <View style={styles.headingBlock}>
        {badge && <View style={styles.badgeWrap}>{badge}</View>}
        {title && <Text accessibilityRole="header" style={[styles.title, { color: c.text }]}>{title}</Text>}
        {subtitle && <Text style={[styles.subtitle, { color: c.muted }]}>{subtitle}</Text>}
      </View>}
      <View style={styles.body}>{children}</View>
      {footerNote ? <View style={[styles.footer, { borderTopColor: c.border }]}>
        <SymbolView name={{ ios: 'lock.fill', android: 'lock', web: 'lock' }} size={14} tintColor={c.muted} />
        <Text style={[styles.footerCopy, { color: c.muted }]}>{footerNote}</Text>
      </View> : null}
    </View>;
  return <View testID="auth-notebook-cover" style={[styles.cover, { backgroundColor: book.cover }, webImage(book.coverGradient)]}>
    {front}
  </View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1 },
  scroll: { flexGrow: 1, paddingHorizontal: 12, paddingTop: 20, paddingBottom: 24 },
  content: { alignSelf: 'center', width: '100%', maxWidth: 560 },
  cover: { width: '100%', borderRadius: 20, paddingLeft: 8, paddingRight: 8, paddingTop: 8, paddingBottom: 12 },
  page: { position: 'relative', borderTopLeftRadius: 5, borderBottomLeftRadius: 5, borderTopRightRadius: 15, borderBottomRightRadius: 15, paddingLeft: 26, paddingRight: 12, paddingVertical: 24, gap: 20 },
  pageCurve: { position: 'absolute', left: 0, top: 0, bottom: 0, width: 20, pointerEvents: 'none' },
  binding: { position: 'absolute', left: -4, top: 0, bottom: 0, width: 18, pointerEvents: 'none' },
  ringUnit: { position: 'absolute', width: 22, height: 12, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', opacity: 0.7 },
  hole: { width: 5, height: 5, borderRadius: 3 },
  ring: { position: 'absolute', left: 2, right: 2, height: 5, borderRadius: 3, boxShadow: '0 1px 2px rgba(0,0,0,0.25)' },
  brandHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 12, paddingBottom: 20, borderBottomWidth: 1 },
  logo: { width: 54, height: 54 }, brandCopy: { flexShrink: 1 },
  wordmark: { ...fontStyles.bold, fontSize: 28, lineHeight: 36, letterSpacing: -0.8 },
  brandCaption: { ...fontStyles.bold, fontSize: 10, lineHeight: 16, letterSpacing: 1.1 },
  headingBlock: { alignItems: 'center', gap: 6 }, badgeWrap: { marginBottom: 4 },
  title: { ...fontStyles.display, fontSize: 30, lineHeight: 38, letterSpacing: -0.6, textAlign: 'center' },
  subtitle: { ...fontStyles.body, fontSize: 13, lineHeight: 20, textAlign: 'center' },
  body: { width: '100%' },
  footer: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: 8, borderTopWidth: 1, paddingTop: 14, flexWrap: 'wrap' },
  footerCopy: { ...fontStyles.body, fontSize: 11, lineHeight: 18 },
});
