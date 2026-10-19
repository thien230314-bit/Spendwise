import { Redirect, router } from 'expo-router';
import type { ComponentProps } from 'react';
import { forwardRef, useEffect, useRef, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import type { ViewStyle } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { checkConnectivity, type ConnectivityState } from '@/api/connectivity';
import { useAuth } from '@/auth/AuthProvider';
import { AuthFormSurface } from './AuthFormSurface';
import { AuthButton, AuthMessage, useAuthColors } from '@/components/auth/AuthUI';
import { UIIcon, type IconName } from '@/components/MobileUI';
import { environment } from '@/config/environment';
import { fontStyles } from '@/theme/fonts';
import { authEffects } from '@/theme/authTokens';
import { errorMessage } from '@/utils/errorMessage';

const connectivityCopy: Partial<Record<ConnectivityState, string>> = {
  'not-configured': 'Chưa cấu hình API. Không thể đăng nhập lúc này.',
  unreachable: 'Không thể kết nối API từ thiết bị này. Vui lòng thử lại.',
};

export default function LoginForm({ active, onBusyChange }: { active: boolean; onBusyChange: (busy: boolean) => void }) {
  const { status, login } = useAuth();
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [connectivity, setConnectivity] = useState<ConnectivityState>('checking');
  const passwordInput = useRef<TextInput>(null);
  const submitting = useRef(false);
  useEffect(() => { onBusyChange(busy); }, [busy, onBusyChange]);

  useEffect(() => {
    let active = true;
    checkConnectivity().then(result => {
      if (active) setConnectivity(result);
    });
    return () => {
      active = false;
    };
  }, []);

  if (active && status === 'authenticated') return <Redirect href="/(authenticated)/home" />;
  if (active && status === 'challenge-pending') return <Redirect href="/(guest)/pin" />;

  const submit = async () => {
    if (submitting.current || !environment.isApiConfigured) return;
    setError('');
    if (!identifier.trim()) {
      setError('Vui lòng nhập Email hoặc số điện thoại.');
      return;
    }
    if (!password) {
      setError('Vui lòng nhập mật khẩu.');
      passwordInput.current?.focus();
      return;
    }
    submitting.current = true;
    setBusy(true);
    try {
      await login(identifier.trim(), password);
      setPassword('');
      router.replace('/(guest)/pin');
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  };

  return (
      <AuthFormSurface
        title="Đăng Nhập"
        subtitle="Truy cập không gian quản lý tài chính SpendWise"
        footerNote="Bảo mật tài chính cá nhân"
      >
        <View style={styles.form}>
          <AuthMessage>{error}</AuthMessage>
          <LoginField
            icon="mail"
            label="Email / Số điện thoại / SpendId"
            value={identifier}
            onChangeText={setIdentifier}
            editable={!busy}
            autoCapitalize="none"
            autoComplete="username"
            autoCorrect={false}
            returnKeyType="next"
            onSubmitEditing={() => passwordInput.current?.focus()}
            placeholder="Email, số điện thoại hoặc SpendId"
          />
          <LoginField
            ref={passwordInput}
            icon="key"
            label="Mật khẩu"
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            reveal
            editable={!busy}
            autoCapitalize="none"
            autoComplete="current-password"
            autoCorrect={false}
            returnKeyType="done"
            onSubmitEditing={submit}
            placeholder="••••••••"
          />
          <LoginLink
            label="Quên mật khẩu?"
            disabled={busy}
            onPress={() => router.push('/(guest)/forgot-password')}
          />
          <AuthButton
            label="Đăng Nhập"
            loadingLabel="Đang xác thực..."
            onPress={submit}
            loading={busy}
            disabled={!environment.isApiConfigured}
          />
          <AuthMessage>{connectivityCopy[connectivity]}</AuthMessage>
        </View>
      </AuthFormSurface>
  );
}

type LoginFieldProps = ComponentProps<typeof TextInput> & { label: string; icon: IconName; reveal?: boolean };
const LoginField = forwardRef<TextInput, LoginFieldProps>(function LoginField(
  { label, icon, reveal = false, style, onFocus, onBlur, secureTextEntry, ...props },
  ref
) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);
  return (
    <View style={styles.field}>
      <Text style={[styles.label, { color: c.label }]}>{label}</Text>
      <View style={styles.fieldWrap}>
        <TextInput
          ref={ref}
          {...props}
          accessibilityLabel={props.accessibilityLabel ?? label}
          secureTextEntry={secureTextEntry && !visible}
          placeholderTextColor={c.placeholder}
          selectionColor={c.focus}
          onFocus={e => {
            setFocused(true);
            onFocus?.(e);
          }}
          onBlur={e => {
            setFocused(false);
            onBlur?.(e);
          }}
          style={[
            styles.input,
            {
              color: c.text,
              backgroundColor: c.input,
              borderColor: focused ? c.focus : c.border,
              boxShadow: focused ? '0 0 0 2px rgba(16,185,129,0.2)' : '0 1px 2px rgba(15,23,42,0.03)',
            },
            reveal && { paddingRight: 52 },
            style,
          ]}
        />
        <View aria-hidden accessible={false} style={styles.icon}>
          <UIIcon name={icon} size={18} color={focused ? c.link : c.placeholder} />
        </View>
        {reveal && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
            accessibilityState={{ disabled: props.editable === false }}
            disabled={props.editable === false}
            onPress={() => setVisible(v => !v)}
            style={({ pressed }) => [styles.reveal, { opacity: pressed ? 0.6 : 1 }]}
          >
            <View aria-hidden accessible={false}>
              <SymbolView
                name={{
                  ios: visible ? 'eye.slash' : 'eye',
                  android: visible ? 'visibility_off' : 'visibility',
                  web: visible ? 'visibility_off' : 'visibility',
                }}
                size={20}
                tintColor={c.muted}
              />
            </View>
          </Pressable>
        )}
      </View>
    </View>
  );
});

function LoginLink({ label, onPress, disabled, secondary = false }: {
  label: string; onPress: () => void; disabled: boolean; secondary?: boolean;
}) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  return <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled }} disabled={disabled} onPress={onPress}
    onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
    style={({ pressed }) => [secondary ? styles.secondary : styles.forgot, { backgroundColor: secondary ? c.registration : pressed ? c.soft : 'transparent', borderColor: focused ? c.focus : c.registrationBorder, boxShadow: focused ? '0 0 0 2px rgba(16,185,129,0.2)' : 'none', opacity: disabled ? 0.5 : pressed ? 0.85 : 1 }, secondary && Platform.OS === 'web' && { backgroundImage: authEffects.registration } as ViewStyle]}>
    {secondary && <View aria-hidden accessible={false}><SymbolView name={{ ios: 'arrow.left', android: 'arrow_back', web: 'arrow_back' }} size={18} tintColor={c.onRegistration} /></View>}
    <Text style={[styles.link, { color: secondary ? c.onRegistration : c.link, textAlign: 'center', flexShrink: 1 }]}>{label}</Text>
  </Pressable>;
}

const styles = StyleSheet.create({
  form: { gap: 16 },
  field: { gap: 4 },
  fieldWrap: { position: 'relative' },
  label: { ...fontStyles.semibold, fontSize: 11, lineHeight: 18, letterSpacing: 0.65, textTransform: 'uppercase' },
  input: {
    ...fontStyles.medium,
    fontSize: 16,
    lineHeight: 24,
    borderRadius: 10,
    borderWidth: 1,
    minHeight: 48,
    paddingLeft: 42,
    paddingRight: 14,
    paddingVertical: 11,
  },
  icon: { position: 'absolute', left: 14, top: 15, pointerEvents: 'none' },
  reveal: { position: 'absolute', right: 2, top: 0, width: 48, height: 48, alignItems: 'center', justifyContent: 'center' },
  forgot: { alignSelf: 'flex-end', borderRadius: 4, minHeight: 44, paddingHorizontal: 8, justifyContent: 'center', marginTop: -8, marginBottom: -8 },
  link: { ...fontStyles.medium, fontSize: 13, lineHeight: 20 },
  secondary: { minHeight: 48, borderWidth: 1, borderRadius: 12, gap: 8, flexDirection: 'row', paddingHorizontal: 12, paddingVertical: 10, justifyContent: 'center', alignItems: 'center' },
});
