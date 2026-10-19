import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { authApi } from '@/api/authApi';
import { AuthPanel } from '@/components/AuthPanel';
import { AuthButton, AuthField, AuthHeading, AuthLink, AuthMessage, AuthText } from '@/components/auth/AuthUI';
import { AuthNotebookScreen as Screen } from '@/components/AuthNotebookShell';
import { environment } from '@/config/environment';
import { validEmail, validPhone } from '@/utils/authValidation';
import { errorMessage } from '@/utils/errorMessage';

export default function AccountRecoveryScreen() {
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [emailHint, setEmailHint] = useState('');
  const [description, setDescription] = useState('');
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const submit = async () => {
    if (submitting.current) return;
    setError('');
    if (!fullName.trim() || fullName.trim().length > 160 || !validPhone(phone) || !description.trim() || description.trim().length > 2000 || (emailHint.trim() && !validEmail(emailHint))) { setError('Vui lòng nhập họ tên, số điện thoại hợp lệ và mô tả tình trạng tài khoản.'); return; }
    submitting.current = true; setBusy(true);
    try { const result = await authApi.requestAccountRecovery({ fullName: fullName.trim(), phone, description: description.trim(), ...(emailHint.trim() ? { emailHint: emailHint.trim() } : {}) }); setNotice(result.message); }
    catch (reason) { setError(errorMessage(reason)); }
    finally { submitting.current = false; setBusy(false); }
  };
  return <Screen><AuthPanel compact>
    <AuthHeading title="Khôi phục tài khoản" subtitle="Dành cho trường hợp bạn không còn truy cập được email đã đăng ký hoặc cần quản trị viên hỗ trợ." />
    <AuthMessage>{error}</AuthMessage>
    {notice ? <AuthMessage success>{notice}</AuthMessage> : <>
      <AuthText>Yêu cầu sẽ được xem xét; việc gửi yêu cầu không tự động đặt lại mật khẩu.</AuthText>
      <AuthField label="Họ và tên" value={fullName} onChangeText={setFullName} editable={!busy} autoComplete="name" placeholder="Nguyễn Văn A" />
      <AuthField label="Số điện thoại liên hệ" value={phone} onChangeText={value => setPhone(value.replace(/\D/g, ''))} editable={!busy} keyboardType="phone-pad" placeholder="0xxxxxxxxx" />
      <AuthField label="Email đăng ký cũ (nếu nhớ)" value={emailHint} onChangeText={setEmailHint} editable={!busy} autoCapitalize="none" autoCorrect={false} keyboardType="email-address" placeholder="example@gmail.com" />
      <AuthField label="Mô tả tình huống" value={description} onChangeText={setDescription} editable={!busy} multiline style={{ minHeight: 112, textAlignVertical: 'top' }} placeholder="Tôi không còn truy cập được email đã đăng ký..." />
      <AuthButton label="Gửi yêu cầu hỗ trợ" onPress={submit} loading={busy} disabled={!environment.isApiConfigured} />
    </>}
    {!environment.isApiConfigured && <AuthMessage>Chưa cấu hình API. Không thể gửi yêu cầu lúc này.</AuthMessage>}
    <AuthLink back label="Quay lại quên mật khẩu" disabled={busy} outlined onPress={() => router.replace('/(guest)/forgot-password')} />
  </AuthPanel></Screen>;
}
