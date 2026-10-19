import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { View } from 'react-native';
import { authApi } from '@/api/authApi';
import { AuthPanel } from '@/components/AuthPanel';
import { AuthButton, AuthField, AuthHeading, AuthLink, AuthMessage, AuthText } from '@/components/auth/AuthUI';
import { AuthNotebookScreen as Screen } from '@/components/AuthNotebookShell';
import { environment } from '@/config/environment';
import { errorMessage } from '@/utils/errorMessage';
import { validEmail, validPassword, validPhone, passwordGuidance } from '@/utils/authValidation';

export default function ForgotPasswordScreen() {
  const [stage, setStage] = useState<'request' | 'otp' | 'complete'>('request');
  const [identifier, setIdentifier] = useState('');
  const [otp, setOtp] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const submit = async () => {
    if (submitting.current) return;
    setError('');
    const clean = identifier.trim();
    if (stage === 'request' && !validEmail(clean) && !validPhone(clean)) { setError('Vui lòng nhập email hoặc số điện thoại hợp lệ.'); return; }
    if (stage === 'otp' && (!/^\d{8}$/.test(otp) || !validPassword(password) || password !== confirm)) { setError('Nhập OTP 8 chữ số và xác nhận mật khẩu hợp lệ. ' + passwordGuidance); return; }
    submitting.current = true; setBusy(true);
    try {
      if (stage === 'request') { const response = await authApi.forgotPassword(clean); setNotice(response.message); setIdentifier(clean); setStage('otp'); }
      else { await authApi.resetPassword(clean, otp, password); setPassword(''); setConfirm(''); setOtp(''); setStage('complete'); }
    } catch (reason) { setError(errorMessage(reason)); }
    finally { submitting.current = false; setBusy(false); }
  };
  return <Screen><AuthPanel compact>
    <AuthHeading title={stage === 'complete' ? 'Đổi mật khẩu thành công' : 'Quên mật khẩu?'} subtitle={stage === 'request' ? 'Nhập email hoặc số điện thoại đã đăng ký. Chúng tôi sẽ gửi mã OTP để đặt lại mật khẩu.' : stage === 'otp' ? 'Nhập mã OTP 8 chữ số và mật khẩu mới của bạn.' : 'Bạn có thể đăng nhập bằng mật khẩu mới.'} />
    <View style={{ gap: 18 }}>
      <AuthMessage>{error}</AuthMessage><AuthMessage success>{notice && stage !== 'complete' ? notice : ''}</AuthMessage>
      {stage === 'request' && <AuthField icon="mail" label="Email hoặc số điện thoại" value={identifier} onChangeText={setIdentifier} editable={!busy} autoCapitalize="none" autoCorrect={false} placeholder="example@gmail.com hoặc 0901234567" />}
      {stage === 'otp' && <>
        <AuthText>{identifier}</AuthText>
        <AuthField label="Mã OTP (8 chữ số)" value={otp} onChangeText={value => setOtp(value.replace(/\D/g, '').slice(0, 8))} editable={!busy} keyboardType="number-pad" autoComplete="one-time-code" maxLength={8} placeholder="Nhập mã 8 chữ số" />
        <AuthField label="Mật khẩu mới" value={password} onChangeText={setPassword} editable={!busy} secureTextEntry autoComplete="new-password" autoCapitalize="none" placeholder="Mật khẩu mới" />
        <AuthText>{passwordGuidance}</AuthText>
        <AuthField label="Xác nhận mật khẩu mới" value={confirm} onChangeText={setConfirm} editable={!busy} secureTextEntry autoComplete="new-password" autoCapitalize="none" placeholder="Nhập lại mật khẩu mới" />
      </>}
      {stage !== 'complete' && <AuthButton label={stage === 'request' ? 'Gửi mã OTP' : 'Xác nhận đổi mật khẩu'} onPress={submit} loading={busy} disabled={!environment.isApiConfigured || (stage === 'otp' && (otp.length !== 8 || !password || !confirm))} />}
      {stage === 'request' && <AuthLink label="Không thể truy cập email? Gửi yêu cầu hỗ trợ" disabled={busy} onPress={() => router.push('/(guest)/account-recovery')} />}
      {stage === 'otp' && <AuthLink label="Quay lại" disabled={busy} onPress={() => { setStage('request'); setNotice(''); setOtp(''); setPassword(''); setConfirm(''); }} />}
      {!environment.isApiConfigured && <AuthMessage>Chưa cấu hình API. Không thể gửi yêu cầu lúc này.</AuthMessage>}
      <AuthLink back label="Quay lại đăng nhập" disabled={busy} outlined onPress={() => router.replace('/(guest)/login')} />
    </View>
  </AuthPanel></Screen>;
}
