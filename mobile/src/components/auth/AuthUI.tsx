import type { ComponentProps, PropsWithChildren } from 'react';
import { forwardRef, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import type { ViewStyle } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { UIIcon, type IconName } from '../MobileUI';
import { authDark, authEffects, authLight, authType } from '@/theme/authTokens';
import { useTheme } from '@/theme/useTheme';

export function useAuthColors() { return useTheme().isDark ? authDark : authLight; }
export function AuthHeading({ title, subtitle }: { title: string; subtitle: string }) {
  const c = useAuthColors();
  return <View style={styles.heading}><Text accessibilityRole="header" style={[authType.title, styles.center, { color: c.text }]}>{title}</Text><Text style={[authType.subtitle, styles.center, { color: c.muted }]}>{subtitle}</Text></View>;
}
export function AuthText({ children }: PropsWithChildren) {
  const c = useAuthColors();
  return <Text style={[authType.body, { color: c.muted }]}>{children}</Text>;
}
export function AuthMessage({ children, success = false }: PropsWithChildren<{ success?: boolean }>) {
  const c = useAuthColors();
  if (!children) return null;
  return <View style={[styles.message, { backgroundColor: success ? c.soft : c.errorBackground, borderColor: success ? c.link : c.errorBorder }]}><Text accessibilityRole="alert" accessibilityLiveRegion="polite" style={[authType.subtitle, { color: success ? c.link : c.error }]}>{children}</Text></View>;
}
type FieldProps = ComponentProps<typeof TextInput> & { label: string; icon?: IconName; reveal?: boolean; error?: string };
export const AuthField = forwardRef<TextInput, FieldProps>(function AuthField({ label, icon, reveal = false, error, style, onFocus, onBlur, secureTextEntry, ...props }, ref) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);
  return <View style={styles.field}><Text style={[authType.label, { color: c.label }]}>{label}</Text><View style={styles.fieldWrap}>
    <TextInput ref={ref} {...props} accessibilityLabel={props.accessibilityLabel ?? label} secureTextEntry={secureTextEntry && !visible} placeholderTextColor={c.placeholder} selectionColor={c.focus}
      onFocus={e => { setFocused(true); onFocus?.(e); }} onBlur={e => { setFocused(false); onBlur?.(e); }}
      accessibilityHint={error || props.accessibilityHint}
      style={[authType.input, styles.input, { color: c.text, backgroundColor: c.input, borderColor: error ? c.errorBorder : focused ? c.focus : c.border, boxShadow: focused ? '0 0 0 2px rgba(16,185,129,0.2)' : '0 1px 2px rgba(15,23,42,0.03)' }, icon && { paddingLeft: 42 }, reveal && { paddingRight: 52 }, style]} />
    {icon && <View style={styles.icon}><UIIcon name={icon} size={18} color={focused ? c.link : c.placeholder} /></View>}
    {reveal && <Pressable accessibilityRole="button" accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} accessibilityState={{ disabled: props.editable === false }} disabled={props.editable === false} onPress={() => setVisible(v => !v)} style={({ pressed }) => [styles.reveal, { opacity: pressed ? 0.6 : 1 }]}><View aria-hidden accessible={false}><SymbolView name={{ ios: visible ? 'eye.slash' : 'eye', android: visible ? 'visibility_off' : 'visibility', web: visible ? 'visibility_off' : 'visibility' }} size={20} tintColor={c.muted} /></View></Pressable>}
  </View>{error && <Text accessibilityRole="alert" style={[authType.subtitle, { color: c.error }]}>{error}</Text>}</View>;
});
export function AuthButton({ label, loadingLabel = 'Đang xử lý...', loading = false, disabled = false, onPress }: { label: string; loadingLabel?: string; loading?: boolean; disabled?: boolean; onPress: () => void }) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  const blocked = disabled || loading;
  return <Pressable accessibilityRole="button" accessibilityLabel={loading ? loadingLabel : label} accessibilityState={{ disabled: blocked, busy: loading }} disabled={blocked} onPress={onPress} onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
    style={({ pressed }) => [styles.button, { backgroundColor: c.button, boxShadow: focused ? '0 0 0 2px rgba(16,185,129,0.5)' : '0 7px 16px rgba(6,78,59,0.3)', opacity: blocked ? 0.6 : pressed ? 0.85 : 1 }, Platform.OS === 'web' && { backgroundImage: authEffects.button } as ViewStyle]}>
    <Text style={[authType.control, { color: c.onButton }]}>{loading ? loadingLabel : label}</Text>
  </Pressable>;
}
export function AuthLink({ label, onPress, disabled = false, outlined = false, back = false }: { label: string; onPress: () => void; disabled?: boolean; outlined?: boolean; back?: boolean }) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  return <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled }} disabled={disabled} onPress={onPress} onFocus={() => setFocused(true)} onBlur={() => setFocused(false)} style={({ pressed }) => [styles.link, outlined && styles.outlined, { borderColor: focused ? c.focus : c.border, backgroundColor: pressed ? c.soft : 'transparent', boxShadow: focused ? '0 0 0 2px rgba(16,185,129,0.2)' : 'none', opacity: disabled ? 0.5 : 1 }]}>
    {back && <View aria-hidden accessible={false}><SymbolView name={{ ios: 'arrow.left', android: 'arrow_back', web: 'arrow_back' }} size={18} tintColor={c.link} /></View>}
    <Text style={[authType.link, { color: c.link, textAlign: 'center', flexShrink: 1 }]}>{label}</Text>
  </Pressable>;
}
const styles = StyleSheet.create({
  heading: { gap: 8, marginBottom: 8 }, center: { textAlign: 'center' }, field: { gap: 6 }, fieldWrap: { position: 'relative' },
  input: { borderRadius: 10, borderWidth: 1, minHeight: 54, paddingHorizontal: 14, paddingVertical: 13 },
  icon: { position: 'absolute', left: 14, top: 18, pointerEvents: 'none' }, reveal: { position: 'absolute', right: 2, top: 3, width: 48, height: 48, alignItems: 'center', justifyContent: 'center' },
  button: { minHeight: 54, padding: 14, borderRadius: 10, borderWidth: 1, borderColor: 'rgba(52,211,153,0.2)', flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, boxShadow: '0 7px 16px rgba(6,78,59,0.3)' },
  link: { minHeight: 48, paddingVertical: 12, paddingHorizontal: 8, borderRadius: 12, flexDirection: 'row', gap: 8, justifyContent: 'center', alignItems: 'center' }, outlined: { borderWidth: 1, borderRadius: 12, paddingHorizontal: 12 },
  message: { borderWidth: 1, borderRadius: 10, padding: 12 },
});
