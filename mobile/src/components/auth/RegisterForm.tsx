import { Redirect, router } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { authApi } from '@/api/authApi';
import { useAuth } from '@/auth/AuthProvider';
import { AuthLayout } from './AuthLayout';
import { AuthButton, AuthField, AuthLink, AuthMessage, AuthText } from '@/components/auth/AuthUI';
import { type TeddyBearHandle, calculateLookFromText } from '../TeddyBearAvatar';
import { environment } from '@/config/environment';
import type { EmailVerificationPendingResponse } from '@/types/auth';
import { passwordGuidance, validEmail, validName, validPassword, validPhone } from '@/utils/authValidation';
import { errorMessage } from '@/utils/errorMessage';

export default function RegisterForm({
  active,
  onBusyChange,
  onSwitchToLogin,
}: {
  active: boolean;
  onBusyChange: (busy: boolean) => void;
  onSwitchToLogin?: () => void;
}) {
  const { status, verifyRegistrationEmail } = useAuth();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [otp, setOtp] = useState('');
  const [pending, setPending] = useState<EmailVerificationPendingResponse | null>(null);
  const [resendAt, setResendAt] = useState(0);
  const [now, setNow] = useState(Date.now);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const submitting = useRef(false);
  const bearRef = useRef<TeddyBearHandle>(null);

  useEffect(() => {
    onBusyChange(busy);
  }, [busy, onBusyChange]);

  useEffect(() => {
    if (!pending) return;
    const clock = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(clock);
  }, [pending]);

  if (active && status === 'authenticated') return <Redirect href="/(authenticated)/home" />;
  if (active && status === 'challenge-pending') return <Redirect href="/(guest)/pin" />;

  const valid =
    validName(fullName) &&
    validEmail(email) &&
    validPhone(phone) &&
    validPassword(password) &&
    password === confirm;

  const cooldown = Math.max(0, Math.ceil((resendAt - now) / 1000));
  const expired = Boolean(pending && Date.parse(pending.expiresAt) <= now);

  const submit = async () => {
    if (submitting.current) return;
    setError('');
    setNotice('');
    if (!pending && !valid) {
      setError('Vui lòng kiểm tra thông tin bắt buộc. ' + passwordGuidance);
      bearRef.current?.triggerFail();
      return;
    }
    if (pending && (!/^\d{8}$/.test(otp) || expired)) {
      setError('Nhập OTP 8 chữ số còn hiệu lực.');
      bearRef.current?.triggerFail();
      return;
    }
    submitting.current = true;
    setBusy(true);
    try {
      if (pending) {
        await verifyRegistrationEmail(email.trim(), otp);
        setOtp('');
        bearRef.current?.triggerSuccess();
        router.replace('/(guest)/pin');
      } else {
        const response = await authApi.register({
          fullName: fullName.trim().replace(/\s+/g, ' '),
          email: email.trim(),
          phone,
          password,
          confirmPassword: confirm,
        });
        setPending(response);
        setResendAt(Date.now() + response.resendAfterSeconds * 1000);
        setNow(Date.now());
        setPassword('');
        setConfirm('');
        bearRef.current?.triggerSuccess();
      }
    } catch (reason) {
      bearRef.current?.triggerFail();
      setError(errorMessage(reason));
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  };

  const resend = async () => {
    if (submitting.current || cooldown > 0) return;
    submitting.current = true;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const response = await authApi.resendRegistrationEmail(email.trim());
      setPending(response);
      setResendAt(Date.now() + response.resendAfterSeconds * 1000);
      setNow(Date.now());
      setNotice('Đã gửi lại mã xác thực.');
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      ref={bearRef}
      title={pending ? 'Xác thực Email' : 'Tạo tài khoản'}
      subtitle={
        pending
          ? 'Nhập mã OTP 8 chữ số được gửi đến email của bạn để tiếp tục thiết lập PIN.'
          : 'Bắt đầu quản trị ngân sách thông minh cùng SpendWise'
      }
      bearVariant="register"
    >
      <View style={styles.form}>
        <AuthMessage>{error}</AuthMessage>
        <AuthMessage success>{notice}</AuthMessage>

        {pending ? (
          <>
            <AuthText>{email}</AuthText>
            <AuthField
              label="Mã OTP (8 chữ số)"
              value={otp}
              editable={!busy}
              onChangeText={(value) => setOtp(value.replace(/\D/g, '').slice(0, 8))}
              keyboardType="number-pad"
              autoComplete="one-time-code"
              maxLength={8}
              placeholder="Nhập mã 8 chữ số"
            />
            {expired && <AuthMessage>Mã xác thực đã hết hạn. Vui lòng gửi lại mã.</AuthMessage>}
            <AuthButton
              label="Xác thực Email"
              loadingLabel="Đang xác thực..."
              loading={busy}
              disabled={!environment.isApiConfigured || otp.length !== 8 || expired}
              onPress={submit}
            />
            <AuthLink
              label={cooldown ? `Gửi lại mã sau ${cooldown} giây` : 'Gửi lại mã OTP'}
              disabled={busy || cooldown > 0 || !environment.isApiConfigured}
              onPress={resend}
            />
          </>
        ) : (
          <>
            <AuthField
              icon="profile"
              label="Họ và tên"
              value={fullName}
              editable={!busy}
              onChangeText={(text) => {
                setFullName(text);
                bearRef.current?.lookAt(calculateLookFromText(text));
              }}
              onFocus={() => bearRef.current?.lookAt(calculateLookFromText(fullName))}
              onBlur={() => {
                setTouched((t) => ({ ...t, name: true }));
                bearRef.current?.setIdle();
              }}
              error={
                touched.name && !validName(fullName)
                  ? 'Họ và tên phải có 2–100 ký tự và chỉ gồm chữ.'
                  : undefined
              }
              autoComplete="name"
              placeholder="Nguyễn Văn A"
            />

            <AuthField
              icon="mail"
              label="Email"
              value={email}
              editable={!busy}
              onChangeText={(text) => {
                setEmail(text);
                bearRef.current?.lookAt(calculateLookFromText(text));
              }}
              onFocus={() => bearRef.current?.lookAt(calculateLookFromText(email))}
              onBlur={() => {
                setTouched((t) => ({ ...t, email: true }));
                bearRef.current?.setIdle();
              }}
              error={
                touched.email && !validEmail(email)
                  ? 'Vui lòng nhập email hợp lệ.'
                  : undefined
              }
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="email"
              keyboardType="email-address"
              placeholder="example@gmail.com"
            />

            <AuthField
              icon="devices"
              label="Số điện thoại"
              value={phone}
              editable={!busy}
              onChangeText={(value) => {
                const cleaned = value.replace(/\D/g, '');
                setPhone(cleaned);
                bearRef.current?.lookAt(calculateLookFromText(cleaned));
              }}
              onFocus={() => bearRef.current?.lookAt(calculateLookFromText(phone))}
              onBlur={() => {
                setTouched((t) => ({ ...t, phone: true }));
                bearRef.current?.setIdle();
              }}
              error={
                touched.phone && !validPhone(phone)
                  ? 'Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.'
                  : undefined
              }
              keyboardType="phone-pad"
              autoComplete="tel"
              placeholder="0xxxxxxxxx"
            />

            <AuthField
              icon="key"
              label="Mật khẩu"
              value={password}
              editable={!busy}
              onChangeText={setPassword}
              onFocus={() => bearRef.current?.setHandsUp(true)}
              onBlur={() => {
                setTouched((t) => ({ ...t, password: true }));
                bearRef.current?.setHandsUp(false);
              }}
              error={
                touched.password && !validPassword(password)
                  ? passwordGuidance
                  : undefined
              }
              secureTextEntry
              reveal
              autoCapitalize="none"
              autoComplete="new-password"
              placeholder="Nhập mật khẩu"
            />

            <AuthField
              icon="key"
              label="Xác nhận mật khẩu"
              value={confirm}
              editable={!busy}
              onChangeText={setConfirm}
              onFocus={() => bearRef.current?.setHandsUp(true)}
              onBlur={() => {
                setTouched((t) => ({ ...t, confirm: true }));
                bearRef.current?.setHandsUp(false);
              }}
              error={
                touched.confirm && (!confirm || confirm !== password)
                  ? 'Mật khẩu xác nhận không khớp.'
                  : undefined
              }
              secureTextEntry
              reveal
              autoCapitalize="none"
              autoComplete="new-password"
              placeholder="Nhập lại mật khẩu"
            />

            <AuthButton
              label="Tạo Tài Khoản Mới"
              loadingLabel="Đang tạo tài khoản..."
              loading={busy}
              disabled={!valid || !environment.isApiConfigured}
              onPress={submit}
            />
          </>
        )}

        {Boolean(onSwitchToLogin) && (
          <View style={styles.switchRow}>
            <Text style={styles.switchPrompt}>Đã có tài khoản?</Text>
            <Pressable
              accessibilityRole="button"
              disabled={busy}
              onPress={onSwitchToLogin}
            >
              <Text style={styles.switchLink}>Đăng nhập ngay</Text>
            </Pressable>
          </View>
        )}

        {!environment.isApiConfigured && (
          <AuthMessage>Chưa cấu hình API. Không thể đăng ký lúc này.</AuthMessage>
        )}

        <View style={styles.footerNote}>
          <Text style={styles.footerText}>Bảo mật tài khoản SpendWise</Text>
        </View>
      </View>
    </AuthLayout>
  );
}

const styles = StyleSheet.create({
  form: {
    gap: 14,
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
