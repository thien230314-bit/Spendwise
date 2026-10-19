import { Redirect, router } from 'expo-router';
import { useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { useAuth } from '@/auth/AuthProvider';
import { AuthNotebookShell, AuthNotebookScreen as Screen } from '@/components/AuthNotebookShell';
import { AuthButton, AuthMessage, useAuthColors } from '@/components/auth/AuthUI';
import { SixDigitPinInput } from '@/components/security/SixDigitPinInput';
import { fontStyles } from '@/theme/fonts';
import { errorMessage } from '@/utils/errorMessage';

export default function PinScreen() {
  const c = useAuthColors();
  const { status, challenge, completePin, cancelChallenge } = useAuth();
  const [pin, setPin] = useState('');
  const [confirmPin, setConfirmPin] = useState('');
  const [setupStep, setSetupStep] = useState<1 | 2>(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [maskPin, setMaskPin] = useState(true);
  const submitting = useRef(false);

  // In development, allow viewing the PIN screen preview even if no live challenge is in memory
  const isPreviewMode = __DEV__ && (status !== 'challenge-pending' || !challenge);

  if (status === 'authenticated') return <Redirect href="/(authenticated)/home" />;
  if (!isPreviewMode && (status !== 'challenge-pending' || !challenge)) {
    return <Redirect href="/(guest)/login" />;
  }

  const isSetup = challenge ? challenge.status === 'PIN_SETUP_REQUIRED' : false;

  const handleCompleteFirstPin = (val: string) => {
    if (isSetup) {
      setPin(val);
      setSetupStep(2);
      setError('');
    } else {
      executeSubmit(val);
    }
  };

  const handleCompleteConfirmPin = (val: string) => {
    executeSubmit(pin, val);
  };

  const executeSubmit = async (pinValue: string, confirmValue?: string) => {
    if (submitting.current) return;
    setError('');

    if (!/^\d{6}$/.test(pinValue)) {
      setError('Mã bảo mật phải gồm đúng 6 chữ số.');
      return;
    }

    if (isSetup) {
      const targetConfirm = confirmValue ?? confirmPin;
      if (pinValue !== targetConfirm) {
        setError('Mã bảo mật xác nhận không khớp.');
        return;
      }
    }

    if (isPreviewMode) {
      setError('Chế độ xem trước giao diện: Vui lòng đăng nhập từ màn hình Login để xác thực thực tế.');
      return;
    }

    submitting.current = true;
    setBusy(true);
    try {
      await completePin(pinValue, isSetup ? (confirmValue ?? confirmPin) : undefined);
      setPin('');
      setConfirmPin('');
      router.replace('/(authenticated)/home');
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  };

  const cancel = () => {
    if (submitting.current) return;
    cancelChallenge();
    router.replace('/(guest)/login');
  };

  const activePinValue = isSetup && setupStep === 2 ? confirmPin : pin;
  const setActivePinValue = (val: string) => {
    if (error) setError('');
    if (isSetup && setupStep === 2) {
      setConfirmPin(val);
    } else {
      setPin(val);
    }
  };

  const currentTitle = isSetup
    ? setupStep === 1
      ? 'Tạo Mã bảo mật (6 số)'
      : 'Xác nhận Mã bảo mật'
    : 'Xác thực Mã bảo mật';

  const currentSubtitle = isSetup
    ? setupStep === 1
      ? 'Thiết lập mã bảo mật 6 số bắt buộc để bảo vệ giao dịch của bạn'
      : 'Nhập lại mã bảo mật 6 số vừa tạo để xác nhận'
    : 'Nhập mã bảo mật 6 số để hoàn tất phiên đăng nhập';

  const shieldBadge = (
    <View style={[styles.shieldIconWrap, { backgroundColor: 'rgba(16,185,129,0.1)', borderColor: 'rgba(16,185,129,0.2)' }]}>
      <SymbolView
        name={{ ios: 'shield.fill', android: 'security', web: 'security' }}
        size={24}
        tintColor={c.link}
      />
    </View>
  );

  return (
    <Screen>
      <AuthNotebookShell
        badge={shieldBadge}
        title={currentTitle}
        subtitle={currentSubtitle}
        footerNote="Xác thực mã bảo mật chuẩn ngân hàng"
      >
        <View style={styles.form}>
          {/* Identity Chip matching Web Auth */}
          <View style={[styles.identityChip, { backgroundColor: c.soft, borderColor: c.border }]}>
            <View style={styles.identityLeft}>
              <View style={[styles.avatarBadge, { backgroundColor: 'rgba(16,185,129,0.15)' }]}>
                <Text style={styles.avatarText}>S</Text>
              </View>
              <View style={styles.identityInfo}>
                <Text style={[styles.identityName, { color: c.text }]} numberOfLines={1}>
                  Tài khoản SpendWise
                </Text>
                <Text style={[styles.identityStatus, { color: c.muted }]}>
                  Đang xác thực bảo mật
                </Text>
              </View>
            </View>
            <View style={[styles.securityBadge, { backgroundColor: c.input, borderColor: c.border }]}>
              <Text style={[styles.securityBadgeText, { color: c.link }]}>BẢO MẬT</Text>
            </View>
          </View>

          <AuthMessage>{error}</AuthMessage>

          {/* 6-Digit PIN slot section */}
          <View style={styles.pinSection}>
            <View style={styles.pinHeader}>
              <Text style={[styles.pinLabel, { color: c.label }]}>
                {isSetup && setupStep === 2 ? 'Xác nhận mã bảo mật' : 'Mã bảo mật (6 số)'}
              </Text>
              {!isSetup && (
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel="Quên mã bảo mật?"
                  disabled={busy}
                  onPress={() => router.push('/(guest)/forgot-pin')}
                  style={({ pressed }) => [{ opacity: pressed ? 0.6 : 1 }]}
                >
                  <Text style={[styles.forgotLink, { color: c.link }]}>Quên mã bảo mật?</Text>
                </Pressable>
              )}
            </View>

            <SixDigitPinInput
              value={activePinValue}
              onChangeText={setActivePinValue}
              onComplete={isSetup && setupStep === 1 ? handleCompleteFirstPin : (val) => {
                if (isSetup && setupStep === 2) {
                  handleCompleteConfirmPin(val);
                } else {
                  handleCompleteFirstPin(val);
                }
              }}
              disabled={busy}
              error={Boolean(error)}
              mask={maskPin}
              showToggle
              onToggleMask={() => setMaskPin((v) => !v)}
              autoFocus
            />

            <Text style={[styles.pinHint, { color: c.muted }]}>
              {busy
                ? 'Đang xác thực bảo mật...'
                : isSetup && setupStep === 1
                ? 'Nhập đủ 6 số để chuyển sang bước xác nhận'
                : 'Tự động xác thực khi nhập đủ 6 số'}
            </Text>
          </View>

          {/* Step Reset if on setup step 2 */}
          {isSetup && setupStep === 2 && (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Nhập lại mã từ bước 1"
              disabled={busy}
              onPress={() => {
                setSetupStep(1);
                setConfirmPin('');
                setError('');
              }}
              style={({ pressed }) => [styles.stepBackButton, { opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.stepBackText, { color: c.link }]}>← Nhập lại mã từ bước 1</Text>
            </Pressable>
          )}

          {/* Explicit Action Button */}
          <AuthButton
            label={isSetup ? (setupStep === 1 ? 'Tiếp tục' : 'Thiết lập và tiếp tục') : 'Xác minh'}
            loadingLabel="Đang xác thực..."
            onPress={() => {
              if (isSetup && setupStep === 1) {
                if (pin.length === 6) {
                  handleCompleteFirstPin(pin);
                } else {
                  setError('Vui lòng nhập đủ 6 chữ số.');
                }
              } else if (isSetup && setupStep === 2) {
                executeSubmit(pin, confirmPin);
              } else {
                executeSubmit(pin);
              }
            }}
            loading={busy}
          />

          {/* Switch Account */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Đổi tài khoản khác"
            accessibilityState={{ disabled: busy }}
            disabled={busy}
            onPress={cancel}
            style={({ pressed }) => [
              styles.cancelButton,
              { borderColor: c.border, opacity: busy ? 0.5 : pressed ? 0.7 : 1 },
            ]}
          >
            <Text style={[styles.cancelText, { color: c.muted }]}>← Đổi tài khoản khác</Text>
          </Pressable>
        </View>
      </AuthNotebookShell>
    </Screen>
  );
}

const styles = StyleSheet.create({
  shieldIconWrap: {
    width: 48,
    height: 48,
    borderRadius: 16,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 4,
  },
  form: { gap: 16 },
  identityChip: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 10,
    borderRadius: 12,
    borderWidth: 1,
  },
  identityLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    flex: 1,
    minWidth: 0,
  },
  avatarBadge: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarText: {
    ...fontStyles.bold,
    fontSize: 14,
    color: '#059669',
  },
  identityInfo: { flex: 1, minWidth: 0 },
  identityName: {
    ...fontStyles.semibold,
    fontSize: 13,
    lineHeight: 18,
  },
  identityStatus: {
    ...fontStyles.body,
    fontSize: 11,
    lineHeight: 16,
  },
  securityBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 10,
    borderWidth: 1,
  },
  securityBadgeText: {
    ...fontStyles.bold,
    fontSize: 9,
    letterSpacing: 0.6,
  },
  pinSection: { gap: 10 },
  pinHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 4,
  },
  pinLabel: {
    ...fontStyles.semibold,
    fontSize: 12,
    lineHeight: 18,
    letterSpacing: 0.5,
    textTransform: 'uppercase',
  },
  forgotLink: {
    ...fontStyles.medium,
    fontSize: 12,
    lineHeight: 18,
  },
  pinHint: {
    ...fontStyles.body,
    fontSize: 11,
    lineHeight: 16,
    textAlign: 'center',
    marginTop: 2,
  },
  stepBackButton: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 6,
  },
  stepBackText: {
    ...fontStyles.medium,
    fontSize: 13,
    lineHeight: 18,
  },
  cancelButton: {
    borderWidth: 1,
    borderRadius: 12,
    minHeight: 46,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 14,
  },
  cancelText: {
    ...fontStyles.medium,
    fontSize: 13,
    lineHeight: 20,
  },
});
