import { useState } from 'react';
import { router } from 'expo-router';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { LogoutButton } from '@/components/LogoutButton';
import { Card, Copy, SettingRow, UIIcon } from '@/components/MobileUI';
import { ProfileModal } from '@/components/ProfileModal';
import { environment } from '@/config/environment';
import { fontStyles, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function MenuScreen() {
  const { user } = useAuth();
  const { colors } = useTheme();
  const [showProfileModal, setShowProfileModal] = useState(false);

  const getFullAvatarUrl = (path?: string | null) => {
    if (!path) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    const base = environment.apiBaseUrl.replace(/\/api\/?$/, '');
    return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
  };

  const fullAvatarUrl = getFullAvatarUrl(user?.avatarUrl);

  return (
    <AppScreen title="Tiện ích" subtitle="Kết nối, hỗ trợ và quản lý tài khoản.">
      <Pressable onPress={() => setShowProfileModal(true)}>
        <Card variant="list">
          <View style={styles.profile}>
            <View style={[styles.avatar, { backgroundColor: colors.primarySoft }]}>
              {fullAvatarUrl ? (
                <Image source={{ uri: fullAvatarUrl }} style={styles.avatarImg} />
              ) : (
                <UIIcon name="profile" size={30} />
              )}
            </View>
            <View style={styles.identity}>
              <Text style={[styles.name, { color: colors.text }]}>{user?.fullName}</Text>
              {user?.spendId ? (
                <Copy muted small>SpendId: {user.spendId}</Copy>
              ) : (
                <Copy muted small>Tài khoản SpendWise</Copy>
              )}
            </View>
            <UIIcon name="chevron" color={colors.textMuted} size={18} />
          </View>
        </Card>
      </Pressable>

      <Copy muted small>Các tiện ích bên dưới sẽ được triển khai ở giai đoạn sau.</Copy>

      <Text accessibilityRole="header" style={[styles.group, { color: colors.textMuted }]}>
        TÀI CHÍNH KẾT NỐI
      </Text>
      <Card variant="list">
        <SettingRow
          icon="friends"
          title="Bạn bè"
          detail="Tìm kiếm, kết bạn và chia sẻ chi tiêu"
          onPress={() => router.push('/(authenticated)/friends')}
        />
        <SettingRow icon="folder" title="Nhóm & Quỹ chi tiêu" detail="Chia sẻ chi tiêu và theo dõi quỹ chung" />
        <SettingRow icon="bank" title="Ngân hàng liên kết" detail="Quản lý tài khoản ngân hàng bên ngoài" />
      </Card>

      <Text accessibilityRole="header" style={[styles.group, { color: colors.textMuted }]}>
        DỊCH VỤ & HỖ TRỢ
      </Text>
      <Card variant="list">
        <SettingRow icon="bell" title="Thông báo" detail="Thông báo tài khoản và giao dịch" />
        <SettingRow icon="help" title="Hỗ trợ & Khiếu nại" detail="Yêu cầu hỗ trợ và tranh chấp giao dịch" />
      </Card>

      <Text accessibilityRole="header" style={[styles.group, { color: colors.textMuted }]}>
        TÀI KHOẢN & BẢO MẬT
      </Text>
      <Card variant="list">
        <SettingRow
          icon="profile"
          title="Hồ sơ cá nhân"
          detail="Thông tin tài khoản, đổi ảnh đại diện và email"
          onPress={() => setShowProfileModal(true)}
        />
        <SettingRow icon="settings" title="Tùy chọn" detail="Giao diện, ngôn ngữ và đơn vị tiền" />
        <SettingRow icon="lock" title="Mã PIN bảo mật" detail="Quản lý mã bảo mật của bạn" />
        <SettingRow icon="devices" title="Thiết bị & Phiên đăng nhập" detail="Theo dõi thiết bị và phiên truy cập" />
      </Card>

      <LogoutButton />

      <ProfileModal visible={showProfileModal} onClose={() => setShowProfileModal(false)} />
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  profile: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
  },
  avatar: {
    width: 48,
    height: 48,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  avatarImg: {
    width: 48,
    height: 48,
    borderRadius: 16,
  },
  identity: {
    flex: 1,
    gap: 4,
  },
  name: {
    ...fontStyles.heading,
    fontSize: 18,
    lineHeight: 26,
  },
  group: {
    fontSize: 12,
    lineHeight: 18,
    ...fontStyles.semibold,
    letterSpacing: 0.6,
  },
});
