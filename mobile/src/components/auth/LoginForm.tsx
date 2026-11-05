import { Redirect, router } from 'expo-router';
import type { ComponentProps } from 'react';
import { forwardRef, useEffect, useRef, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { checkConnectivity, type ConnectivityState } from '@/api/connectivity';
import { useAuth } from '@/auth/AuthProvider';
import { AuthLayout } from './AuthLayout';
import { AuthButton, AuthMessage, useAuthColors } from '@/components/auth/AuthUI';
import { type TeddyBearHandle, calculateLookFromText } from '../TeddyBearAvatar';
import { environment } from '@/config/environment';
import { errorMessage } from '@/utils/errorMessage';

const connectivityCopy: Partial<Record<ConnectivityState, string>> = {
  'not-configured': 'Chưa cấu hình API. Không thể đăng nhập lúc này.',
  unreachable: 'Không thể kết nối API từ thiết bị này. Vui lòng thử lại.',
};

export default function LoginForm({
  active,
  onBusyChange,
  onSwitchToRegister,
}: {
  active: boolean;
  onBusyChange: (busy: boolean) => void;
  onSwitchToRegister?: () => void;
}) {
  const { status, login } = useAuth();
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [connectivity, setConnectivity] = useState<ConnectivityState>('checking');
  const passwordInput = useRef<TextInput>(null);
  const submitting = useRef(false);
  const bearRef = useRef<TeddyBearHandle>(null);

  useEffect(() => {
    onBusyChange(busy);
  }, [busy, onBusyChange]);

  useEffect(() => {
    let isActive = true;
    checkConnectivity().then((result) => {
      if (isActive) setConnectivity(result);
    });
    return () => {
      isActive = false;
    };
  }, []);

  if (active && status === 'authenticated') return <Redirect href="/(authenticated)/home" />;
  if (active && status === 'challenge-pending') return <Redirect href="/(guest)/pin" />;

  const submit = async () => {
    if (submitting.current || !environment.isApiConfigured) return;
    setError('');
    if (!identifier.trim()) {
      setError('Vui lòng nhập Email hoặc số điện thoại.');
      bearRef.current?.triggerFail();
      return;
    }
    if (!password) {
      setError('Vui lòng nhập mật khẩu.');
      bearRef.current?.triggerFail();
      passwordInput.current?.focus();
      return;
    }
    submitting.current = true;
    setBusy(true);
    try {
      await login(identifier.trim(), password);
      bearRef.current?.triggerSuccess();
      setPassword('');
      router.replace('/(guest)/pin');
    } catch (reason) {
      bearRef.current?.triggerFail();
      setError(errorMessage(reason));
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      ref={bearRef}
      title="Đăng Nhập"
      subtitle="Truy cập không gian quản lý tài chính SpendWise"
      bearVariant="login"
    >
      <View style={styles.form}>
        <AuthMessage>{error}</AuthMessage>
        <LoginField
          label="Email hoặc Số điện thoại"
          value={identifier}
          onChangeText={(text) => {
            setIdentifier(text);
            bearRef.current?.lookAt(calculateLookFromText(text));
          }}
          onFocus={() => {
            bearRef.current?.lookAt(calculateLookFromText(identifier));
          }}
          onBlur={() => {
            bearRef.current?.setIdle();
          }}
          editable={!busy}
          autoCapitalize="none"
          autoComplete="username"
          autoCorrect={false}
          returnKeyType="next"
          onSubmitEditing={() => passwordInput.current?.focus()}
          placeholder="name@domain.com"
        />

        <LoginField
          ref={passwordInput}
          label="Mật khẩu"
          value={password}
          onChangeText={setPassword}
          onFocus={() => {
            bearRef.current?.setHandsUp(true);
          }}
          onBlur={() => {
            bearRef.current?.setHandsUp(false);
          }}
          secureTextEntry
          reveal
          editable={!busy}
          autoCapitalize="none"
          autoComplete="current-password"
          autoCorrect={false}
          returnKeyType="done"
          onSubmitEditing={submit}
          placeholder="••••••••••••"
        />

        <View style={styles.actionRow}>
          <Pressable
            accessibilityRole="button"
            disabled={busy}
            onPress={() => router.push('/(guest)/forgot-password')}
          >
            <Text style={styles.forgotLink}>Quên mật khẩu?</Text>
          </Pressable>
        </View>

        <AuthButton
          label="Đăng Nhập"
          loadingLabel="Đang xác thực..."
          onPress={submit}
          loading={busy}
          disabled={!environment.isApiConfigured}
        />

        {Boolean(onSwitchToRegister) && (
          <View style={styles.switchRow}>
            <Text style={styles.switchPrompt}>Chưa có tài khoản?</Text>
            <Pressable
              accessibilityRole="button"
              disabled={busy}
              onPress={onSwitchToRegister}
            >
              <Text style={styles.switchLink}>Đăng ký ngay</Text>
            </Pressable>
          </View>
        )}

        <AuthMessage>{connectivityCopy[connectivity]}</AuthMessage>

        <View style={styles.footerNote}>
          <Text style={styles.footerText}>Bảo mật tài chính cá nhân</Text>
        </View>
      </View>
    </AuthLayout>
  );
}

type LoginFieldProps = ComponentProps<typeof TextInput> & {
  label: string;
  reveal?: boolean;
};

const LoginField = forwardRef<TextInput, LoginFieldProps>(function LoginField(
  { label, reveal = false, style, onFocus, onBlur, secureTextEntry, ...props },
  ref
) {
  const c = useAuthColors();
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);

  return (
    <View style={styles.field}>
      <Text style={[styles.label, { color: '#0B2A22' }]}>{label}</Text>
      <View style={styles.fieldWrap}>
        <TextInput
          ref={ref}
          {...props}
          accessibilityLabel={props.accessibilityLabel ?? label}
          secureTextEntry={secureTextEntry && !visible}
          placeholderTextColor="#8FA39A"
          selectionColor="#0F9D6E"
          onFocus={(e) => {
            setFocused(true);
            onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            onBlur?.(e);
          }}
          style={[
            styles.input,
            {
              color: '#0B2A22',
              backgroundColor: '#FFFFFF',
              borderColor: focused ? '#0F9D6E' : 'rgba(15, 157, 110, 0.2)',
            },
            style,
          ]}
        />
        {reveal && secureTextEntry && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
            onPress={() => setVisible((v) => !v)}
            style={styles.reveal}
          >
            <Text style={styles.revealText}>{visible ? 'Ẩn' : 'Hiện'}</Text>
          </Pressable>
        )}
      </View>
    </View>
  );
});

const styles = StyleSheet.create({
  form: {
    gap: 14,
  },
  field: {
    gap: 6,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
  },
  fieldWrap: {
    position: 'relative',
    justifyContent: 'center',
  },
  input: {
    minHeight: 48,
    borderRadius: 12,
    borderWidth: 1.5,
    paddingHorizontal: 14,
    fontSize: 15,
  },
  reveal: {
    position: 'absolute',
    right: 12,
    height: '100%',
    justifyContent: 'center',
    paddingHorizontal: 6,
  },
  revealText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#0F9D6E',
  },
  actionRow: {
    alignItems: 'flex-end',
    marginTop: -4,
  },
  forgotLink: {
    fontSize: 13,
    fontWeight: '600',
    color: '#0F9D6E',
  },
  switchRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: 6,
    marginTop: 8,
  },
  switchPrompt: {
    fontSize: 14,
    color: '#4B6358',
  },
  switchLink: {
    fontSize: 14,
    fontWeight: '700',
    color: '#0F9D6E',
  },
  footerNote: {
    alignItems: 'center',
    marginTop: 10,
    borderTopWidth: 1,
    borderTopColor: 'rgba(15, 157, 110, 0.1)',
    paddingTop: 12,
  },
  footerText: {
    fontSize: 12,
    color: '#6B8076',
  },
});
