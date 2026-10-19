import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { authApi } from '@/api/authApi';
import { AuthPanel } from '@/components/AuthPanel';
import { AuthButton, AuthField, AuthHeading, AuthLink, AuthMessage, AuthText } from '@/components/auth/AuthUI';
import { AuthNotebookScreen as Screen } from '@/components/AuthNotebookShell';
import { environment } from '@/config/environment';
import { errorMessage } from '@/utils/errorMessage';

export default function ResetPinScreen() {
  const params = useLocalSearchParams<{ token?: string | string[] }>();
  const token = typeof params.token === 'string' ? params.token : '';
  return <ResetPinForm key={token} token={token} />;
}

function ResetPinForm({ token }: { token: string }) {
  const [state, setState] = useState<'checking' | 'valid' | 'invalid' | 'unavailable' | 'complete'>(token ? 'checking' : 'invalid');
  const [pin, setPin] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  useEffect(() => {
    let active = true;
    if (!token) return;
    authApi.validatePinReset(token).then(result => { if (active) setState(result.valid && Date.parse(result.expiresAt) > Date.now() ? 'valid' : 'invalid'); })
      .catch(reason => { if (active) { setError(errorMessage(reason)); setState('unavailable'); } });
    return () => { active = false; };
  }, [token]);
  const submit = async () => {
    if (submitting.current || state !== 'valid') return;
    setError('');
    if (!/^\d{6}$/.test(pin) || pin !== confirm) { setError('Mã bảo mật phải gồm 6 chữ số và khớp với mã xác nhận.'); return; }
    submitting.current = true; setBusy(true);
    try { await authApi.resetPin(token, pin, confirm); setPin(''); setConfirm(''); setState('complete'); }
    catch (reason) { setError(errorMessage(reason)); }
    finally { submitting.current = false; setBusy(false); }
  };
  return <Screen><AuthPanel compact>
    <AuthHeading title={state === 'complete' ? 'Đã đặt lại Mã bảo mật' : 'Đặt lại Mã bảo mật'} subtitle="Tạo Mã bảo mật 6 số mới cho tài khoản SpendWise." />
    <AuthMessage>{error}</AuthMessage>
    {state === 'checking' && <AuthText>Đang kiểm tra liên kết...</AuthText>}
    {state === 'unavailable' && <AuthText>Chưa thể kiểm tra liên kết. Vui lòng thử lại khi kết nối API khả dụng.</AuthText>}
    {state === 'invalid' && <><AuthMessage>Liên kết không hợp lệ, đã hết hạn hoặc đã được sử dụng.</AuthMessage><AuthLink back label="Yêu cầu liên kết mới" onPress={() => router.replace('/(guest)/forgot-pin')} /></>}
    {state === 'complete' && <AuthMessage success>Mã bảo mật mới đã được lưu. Vui lòng đăng nhập lại.</AuthMessage>}
    {state === 'valid' && <>
      <AuthField label="Mã bảo mật mới (6 số)" value={pin} editable={!busy} onChangeText={value => setPin(value.replace(/\D/g, '').slice(0, 6))} keyboardType="number-pad" secureTextEntry maxLength={6} placeholder="••••••" />
      <AuthField label="Xác nhận Mã bảo mật" value={confirm} editable={!busy} onChangeText={value => setConfirm(value.replace(/\D/g, '').slice(0, 6))} keyboardType="number-pad" secureTextEntry maxLength={6} placeholder="••••••" />
      <AuthButton label="Lưu Mã bảo mật mới" onPress={submit} loading={busy} disabled={!environment.isApiConfigured || pin.length !== 6 || confirm.length !== 6} />
    </>}
    <AuthLink back label="Quay lại đăng nhập" disabled={busy} outlined onPress={() => router.replace('/(guest)/login')} />
  </AuthPanel></Screen>;
}
