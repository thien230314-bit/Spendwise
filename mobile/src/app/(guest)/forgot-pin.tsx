import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { authApi } from '@/api/authApi';
import { useAuth } from '@/auth/AuthProvider';
import { AuthPanel } from '@/components/AuthPanel';
import { AuthButton, AuthField, AuthHeading, AuthLink, AuthMessage } from '@/components/auth/AuthUI';
import { AuthNotebookScreen as Screen } from '@/components/AuthNotebookShell';
import { environment } from '@/config/environment';
import { validEmail } from '@/utils/authValidation';
import { errorMessage } from '@/utils/errorMessage';

export default function ForgotPinScreen() {
  const { status, cancelChallenge } = useAuth();
  const [email, setEmail] = useState('');
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const submit = async () => {
    if (submitting.current) return;
    setError('');
    if (!validEmail(email)) { setError('Vui lòng nhập địa chỉ email hợp lệ.'); return; }
    submitting.current = true; setBusy(true);
    try { const result = await authApi.forgotPin(email.trim()); setNotice(result.message); }
    catch (reason) { setError(errorMessage(reason)); }
    finally { submitting.current = false; setBusy(false); }
  };
  return <Screen><AuthPanel compact>
    <AuthHeading title="Khôi phục Mã bảo mật" subtitle="Nhập địa chỉ email tài khoản để nhận liên kết tạo Mã bảo mật 6 số mới. Mã hiện tại vẫn có hiệu lực cho đến khi bạn thiết lập thành công mã mới." />
    <AuthMessage>{error}</AuthMessage>
    {notice ? <AuthMessage success>{notice}</AuthMessage> : <>
      <AuthField icon="mail" label="Email tài khoản" value={email} editable={!busy} onChangeText={setEmail} autoCapitalize="none" autoCorrect={false} keyboardType="email-address" autoComplete="email" placeholder="example@gmail.com" />
      <AuthButton label="Gửi hướng dẫn" onPress={submit} loading={busy} disabled={!environment.isApiConfigured} />
    </>}
    {!environment.isApiConfigured && <AuthMessage>Chưa cấu hình API. Không thể gửi yêu cầu lúc này.</AuthMessage>}
    <AuthLink back label="Quay lại" outlined disabled={busy} onPress={() => router.replace(status === 'challenge-pending' ? '/(guest)/pin' : '/(guest)/login')} />
    <AuthLink back label="Quay lại đăng nhập" disabled={busy} onPress={() => { cancelChallenge(); router.replace('/(guest)/login'); }} />
  </AuthPanel></Screen>;
}
