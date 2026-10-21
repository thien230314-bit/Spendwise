import { useState, useRef } from 'react';
import {
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  Image,
  ActivityIndicator,
} from 'react-native';
import { useAuth } from '@/auth/AuthProvider';
import { userApi } from '@/api/userApi';
import { environment } from '@/config/environment';
import { Card, Copy, UIIcon } from './MobileUI';
import { FormField } from './FormField';
import { PrimaryButton } from './PrimaryButton';
import { fontStyles, radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

type Props = {
  visible: boolean;
  onClose: () => void;
};

export function ProfileModal({ visible, onClose }: Props) {
  const { user, updateUser } = useAuth();
  const { colors } = useTheme();

  // Avatar state
  const [avatarLoading, setAvatarLoading] = useState(false);
  const [avatarError, setAvatarError] = useState<string | null>(null);
  const [avatarSuccess, setAvatarSuccess] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Email change state
  const [isChangingEmail, setIsChangingEmail] = useState(false);
  const [newEmail, setNewEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [emailStep, setEmailStep] = useState<'input' | 'otp'>('input');
  const [emailLoading, setEmailLoading] = useState(false);
  const [emailError, setEmailError] = useState<string | null>(null);
  const [emailSuccess, setEmailSuccess] = useState<string | null>(null);

  const getFullAvatarUrl = (path?: string | null) => {
    if (!path) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    const base = environment.apiBaseUrl.replace(/\/api\/?$/, '');
    return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
  };

  const handlePickFile = () => {
    setAvatarError(null);
    setAvatarSuccess(null);
    if (Platform.OS === 'web' && fileInputRef.current) {
      fileInputRef.current.click();
    }
  };

  const handleFileChange = async (event: any) => {
    const file = event.target?.files?.[0];
    if (!file) return;

    if (file.size > 5 * 1024 * 1024) {
      setAvatarError('Ảnh không được vượt quá 5 MB.');
      return;
    }

    const validTypes = ['image/jpeg', 'image/png', 'image/webp'];
    if (!validTypes.includes(file.type)) {
      setAvatarError('Định dạng không hỗ trợ. Vui lòng chọn ảnh JPG, PNG hoặc WebP.');
      return;
    }

    setAvatarLoading(true);
    setAvatarError(null);
    try {
      const updatedProfile = await userApi.uploadAvatar(file);
      updateUser(updatedProfile);
      setAvatarSuccess('Cập nhật ảnh đại diện thành công!');
    } catch (err: any) {
      setAvatarError(err?.message || 'Không thể tải ảnh đại diện lên.');
    } finally {
      setAvatarLoading(false);
    }
  };

  const handleRequestEmailChange = async () => {
    setEmailError(null);
    setEmailSuccess(null);
    const trimmed = newEmail.trim();
    if (!trimmed) {
      setEmailError('Vui lòng nhập địa chỉ email mới.');
      return;
    }
    if (trimmed.toLowerCase() === user?.email?.toLowerCase()) {
      setEmailError('Email mới không được trùng với email hiện tại.');
      return;
    }

    setEmailLoading(true);
    try {
      await userApi.requestEmailChange(trimmed);
      setEmailStep('otp');
      setEmailSuccess(`Mã xác thực OTP đã được gửi tới ${trimmed}. Vui lòng kiểm tra hộp thư.`);
    } catch (err: any) {
      setEmailError(err?.message || 'Không thể gửi yêu cầu đổi email.');
    } finally {
      setEmailLoading(false);
    }
  };

  const handleConfirmEmailChange = async () => {
    setEmailError(null);
    setEmailSuccess(null);
    const trimmedOtp = otp.trim();
    if (trimmedOtp.length !== 8) {
      setEmailError('Mã OTP phải bao gồm 8 chữ số.');
      return;
    }

    setEmailLoading(true);
    try {
      const updatedProfile = await userApi.confirmEmailChange(trimmedOtp);
      updateUser(updatedProfile);
      setEmailSuccess('Đổi địa chỉ email thành công!');
      setIsChangingEmail(false);
      setEmailStep('input');
      setNewEmail('');
      setOtp('');
    } catch (err: any) {
      setEmailError(err?.message || 'Mã xác thực không hợp lệ hoặc đã hết hạn.');
    } finally {
      setEmailLoading(false);
    }
  };

  const resetEmailFlow = () => {
    setIsChangingEmail(false);
    setEmailStep('input');
    setNewEmail('');
    setOtp('');
    setEmailError(null);
    setEmailSuccess(null);
  };

  const fullAvatarUrl = getFullAvatarUrl(user?.avatarUrl);

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={[styles.container, { backgroundColor: colors.background, borderColor: colors.border }]}>
          {/* Header */}
          <View style={[styles.header, { borderBottomColor: colors.border }]}>
            <Text style={[styles.headerTitle, { color: colors.text }]}>Hồ sơ cá nhân</Text>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Đóng"
              onPress={onClose}
              style={({ pressed }) => [styles.closeBtn, { opacity: pressed ? 0.6 : 1 }]}
            >
              <Text style={[styles.closeText, { color: colors.textMuted }]}>✕</Text>
            </Pressable>
          </View>

          <ScrollView contentContainerStyle={styles.scrollBody} showsVerticalScrollIndicator={false}>
            {/* Hidden web file input */}
            {Platform.OS === 'web' && (
              <input
                ref={fileInputRef as any}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                style={{ display: 'none' }}
                onChange={handleFileChange}
              />
            )}

            {/* Avatar Section */}
            <View style={styles.avatarSection}>
              <View style={[styles.avatarWrap, { borderColor: colors.primary }]}>
                {fullAvatarUrl ? (
                  <Image source={{ uri: fullAvatarUrl }} style={styles.avatarImg} />
                ) : (
                  <View style={[styles.avatarPlaceholder, { backgroundColor: colors.primarySoft }]}>
                    <UIIcon name="profile" size={48} />
                  </View>
                )}
                {avatarLoading && (
                  <View style={styles.loadingOverlay}>
                    <ActivityIndicator size="small" color="#fff" />
                  </View>
                )}
              </View>

              <Pressable
                accessibilityRole="button"
                onPress={handlePickFile}
                disabled={avatarLoading}
                style={({ pressed }) => [
                  styles.changeAvatarBtn,
                  { backgroundColor: colors.primarySoft, opacity: pressed ? 0.7 : 1 },
                ]}
              >
                <Text style={[styles.changeAvatarText, { color: colors.primary }]}>
                  {avatarLoading ? 'Đang tải lên...' : 'Đổi ảnh đại diện'}
                </Text>
              </Pressable>

              {avatarError && <Text style={[styles.errorText, { color: colors.danger }]}>{avatarError}</Text>}
              {avatarSuccess && <Text style={[styles.successText, { color: colors.primary }]}>{avatarSuccess}</Text>}
            </View>

            {/* User details */}
            <Text style={[styles.sectionHeading, { color: colors.textMuted }]}>THÔNG TIN TÀI KHOẢN</Text>
            <Card variant="list">
              <View style={styles.infoRow}>
                <Text style={[styles.infoLabel, { color: colors.textMuted }]}>Họ và tên</Text>
                <Text style={[styles.infoValue, { color: colors.text }]}>{user?.fullName || '—'}</Text>
              </View>
              <View style={[styles.infoRow, { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                <Text style={[styles.infoLabel, { color: colors.textMuted }]}>SpendId</Text>
                <Text style={[styles.infoValue, { color: colors.text }]}>{user?.spendId || '—'}</Text>
              </View>
              <View style={[styles.infoRow, { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                <Text style={[styles.infoLabel, { color: colors.textMuted }]}>Số điện thoại</Text>
                <Text style={[styles.infoValue, { color: colors.text }]}>{user?.phone || 'Chưa cập nhật'}</Text>
              </View>
              <View style={[styles.infoRow, { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                <Text style={[styles.infoLabel, { color: colors.textMuted }]}>Vai trò</Text>
                <Text style={[styles.infoValue, { color: colors.text }]}>
                  {user?.systemRole === 'Admin' || user?.systemRole === 'SystemAdmin' ? 'Quản trị viên' : 'Người dùng'}
                </Text>
              </View>
            </Card>

            {/* Email Management */}
            <Text style={[styles.sectionHeading, { color: colors.textMuted }]}>ĐỊA CHỈ EMAIL</Text>
            <Card variant="panel">
              <View style={styles.emailHeader}>
                <View style={styles.emailBadgeRow}>
                  <Text style={[styles.emailDisplay, { color: colors.text }]}>{user?.email || '—'}</Text>
                  <View style={[styles.verifiedBadge, { backgroundColor: colors.primarySoft }]}>
                    <Text style={[styles.verifiedText, { color: colors.primary }]}>Đã xác thực</Text>
                  </View>
                </View>

                {!isChangingEmail && (
                  <Pressable
                    accessibilityRole="button"
                    onPress={() => setIsChangingEmail(true)}
                    style={({ pressed }) => [
                      styles.emailActionBtn,
                      { borderColor: colors.border, opacity: pressed ? 0.7 : 1 },
                    ]}
                  >
                    <Text style={[styles.emailActionText, { color: colors.primary }]}>Đổi địa chỉ email</Text>
                  </Pressable>
                )}
              </View>

              {isChangingEmail && (
                <View style={[styles.changeEmailForm, { borderTopColor: colors.border }]}>
                  {emailStep === 'input' ? (
                    <>
                      <FormField
                        label="Email mới"
                        icon="mail"
                        placeholder="nhap.email.moi@domain.com"
                        keyboardType="email-address"
                        autoCapitalize="none"
                        value={newEmail}
                        onChangeText={setNewEmail}
                      />
                      {emailError && <Text style={[styles.errorText, { color: colors.danger }]}>{emailError}</Text>}
                      <View style={styles.emailButtonRow}>
                        <Pressable onPress={resetEmailFlow} style={styles.cancelBtn}>
                          <Text style={[styles.cancelText, { color: colors.textMuted }]}>Hủy</Text>
                        </Pressable>
                        <View style={{ flex: 1 }}>
                          <PrimaryButton
                            label="Gửi mã xác thực"
                            onPress={handleRequestEmailChange}
                            loading={emailLoading}
                          />
                        </View>
                      </View>
                    </>
                  ) : (
                    <>
                      <Copy small muted>
                        Mã OTP xác thực 8 chữ số đã được gửi tới email mới.
                      </Copy>
                      <FormField
                        label="Mã OTP xác thực (8 chữ số)"
                        icon="key"
                        placeholder="12345678"
                        keyboardType="number-pad"
                        maxLength={8}
                        value={otp}
                        onChangeText={setOtp}
                      />
                      {emailError && <Text style={[styles.errorText, { color: colors.danger }]}>{emailError}</Text>}
                      {emailSuccess && <Text style={[styles.successText, { color: colors.primary }]}>{emailSuccess}</Text>}
                      <View style={styles.emailButtonRow}>
                        <Pressable onPress={resetEmailFlow} style={styles.cancelBtn}>
                          <Text style={[styles.cancelText, { color: colors.textMuted }]}>Quay lại</Text>
                        </Pressable>
                        <View style={{ flex: 1 }}>
                          <PrimaryButton
                            label="Xác nhận đổi Email"
                            onPress={handleConfirmEmailChange}
                            loading={emailLoading}
                          />
                        </View>
                      </View>
                    </>
                  )}
                </View>
              )}
            </Card>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.6)',
    justifyContent: 'flex-end',
  },
  container: {
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    borderTopWidth: 1,
    maxHeight: '90%',
    minHeight: '60%',
    overflow: 'hidden',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  headerTitle: {
    ...fontStyles.heading,
    fontSize: 18,
  },
  closeBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  closeText: {
    fontSize: 20,
    fontWeight: 'bold',
  },
  scrollBody: {
    padding: spacing.lg,
    gap: spacing.md,
    paddingBottom: spacing.xl * 2,
  },
  avatarSection: {
    alignItems: 'center',
    gap: spacing.sm,
    paddingVertical: spacing.sm,
  },
  avatarWrap: {
    width: 88,
    height: 88,
    borderRadius: 44,
    borderWidth: 2,
    overflow: 'hidden',
    position: 'relative',
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarImg: {
    width: '100%',
    height: '100%',
    resizeMode: 'cover',
  },
  avatarPlaceholder: {
    width: '100%',
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingOverlay: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(0,0,0,0.5)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  changeAvatarBtn: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs + 2,
    borderRadius: radius.md,
  },
  changeAvatarText: {
    ...fontStyles.semibold,
    fontSize: 13,
  },
  sectionHeading: {
    fontSize: 12,
    lineHeight: 18,
    ...fontStyles.semibold,
    letterSpacing: 0.6,
    marginTop: spacing.xs,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm + 2,
  },
  infoLabel: {
    ...fontStyles.body,
    fontSize: 14,
  },
  infoValue: {
    ...fontStyles.semibold,
    fontSize: 14,
  },
  emailHeader: {
    gap: spacing.sm,
  },
  emailBadgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm,
  },
  emailDisplay: {
    ...fontStyles.bold,
    fontSize: 15,
    flex: 1,
  },
  verifiedBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: radius.sm,
  },
  verifiedText: {
    ...fontStyles.semibold,
    fontSize: 11,
  },
  emailActionBtn: {
    alignSelf: 'flex-start',
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: radius.sm,
    borderWidth: 1,
    marginTop: 4,
  },
  emailActionText: {
    ...fontStyles.semibold,
    fontSize: 13,
  },
  changeEmailForm: {
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: StyleSheet.hairlineWidth,
    gap: spacing.sm,
  },
  emailButtonRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    marginTop: spacing.xs,
  },
  cancelBtn: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    minHeight: 48,
    justifyContent: 'center',
  },
  cancelText: {
    ...fontStyles.medium,
    fontSize: 14,
  },
  errorText: {
    fontSize: 12,
    ...fontStyles.medium,
    marginTop: 2,
  },
  successText: {
    fontSize: 12,
    ...fontStyles.medium,
    marginTop: 2,
  },
});
