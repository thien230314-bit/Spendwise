import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  Pressable,
  Alert,
  Image,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { ProfileModal } from '@/components/ProfileModal';
import { environment } from '@/config/environment';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing, typography } from '@/theme/tokens';

export default function ProfileScreen() {
  const { user, logout } = useAuth();
  const { colors, isDark } = useTheme();
  const [showProfileModal, setShowProfileModal] = useState(false);

  const getFullAvatarUrl = (path?: string | null) => {
    if (!path) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    const base = environment.apiBaseUrl.replace(/\/api\/?$/, '');
    return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
  };

  const fullAvatarUrl = getFullAvatarUrl(user?.avatarUrl);
  const spendId = (user as any)?.spendId || (user as any)?.accountNumber || 'SW-USER-8888';
  const fullName = user?.fullName || 'Người dùng SpendWise';
  const email = user?.email || 'user@spendwise.vn';

  const handleCopySpendId = () => {
    Alert.alert('Đã sao chép', `Đã sao chép mã SpendID: ${spendId}`);
  };

  const handleLogout = () => {
    Alert.alert('Đăng xuất', 'Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng SpendWise?', [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Đăng xuất',
        style: 'destructive',
        onPress: async () => {
          await logout();
          router.replace('/(guest)/login');
        },
      },
    ]);
  };

  return (
    <AppScreen title="Cá nhân" scrollable>
      <View style={styles.container}>
        {/* 1. USER PROFILE CARD */}
        <GlassCard style={styles.profileCard}>
          <View style={styles.avatarRow}>
            <View style={[styles.avatarContainer, { backgroundColor: colors.primarySoft }]}>
              {fullAvatarUrl ? (
                <Image source={{ uri: fullAvatarUrl }} style={styles.avatarImage} />
              ) : (
                <Ionicons name="person" size={32} color={colors.primary} />
              )}
            </View>

            <View style={styles.profileInfo}>
              <Text style={[styles.userName, { color: colors.text }]}>{fullName}</Text>
              <Text style={[styles.userEmail, { color: colors.textMuted }]}>{email}</Text>

              {/* SpendId Pill */}
              <Pressable
                onPress={handleCopySpendId}
                style={[styles.spendIdPill, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}
              >
                <Text style={[styles.spendIdLabel, { color: colors.textMuted }]}>SpendID:</Text>
                <Text style={[styles.spendIdVal, { color: colors.primary }]}>{spendId}</Text>
                <Ionicons name="copy-outline" size={14} color={colors.primary} />
              </Pressable>
            </View>

            <Pressable
              onPress={() => setShowProfileModal(true)}
              style={[styles.editProfileBtn, { borderColor: colors.border }]}
            >
              <Ionicons name="pencil" size={16} color={colors.primary} />
            </Pressable>
          </View>
        </GlassCard>

        {/* 2. SECURITY & AUTH SECTION */}
        <Text style={[styles.sectionHeader, { color: colors.textMuted }]}>BẢO MẬT & XÁC THỰC</Text>
        <GlassCard style={styles.menuCard}>
          <Pressable
            onPress={() => router.push('/(guest)/reset-pin')}
            style={[styles.menuItem, { borderBottomColor: colors.border }]}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
              <Ionicons name="shield-checkmark-outline" size={20} color={colors.primary} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Mã PIN bảo mật 6 số</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Bảo vệ giao dịch chuyển tiền & quỹ</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>

          <Pressable
            onPress={() => setShowProfileModal(true)}
            style={styles.menuItem}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#1C382F' : '#E0F2FE' }]}>
              <Ionicons name="key-outline" size={20} color={colors.sky} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Đổi mật khẩu tài khoản</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Cập nhật mật khẩu đăng nhập định kỳ</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>
        </GlassCard>

        {/* 3. FINANCE & CONNECTIONS */}
        <Text style={[styles.sectionHeader, { color: colors.textMuted }]}>TÀI CHÍNH & KẾT NỐI</Text>
        <GlassCard style={styles.menuCard}>
          <Pressable
            onPress={() => router.push('/(authenticated)/wallet')}
            style={[styles.menuItem, { borderBottomColor: colors.border }]}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
              <Ionicons name="wallet-outline" size={20} color={colors.primary} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Tất cả ví & Hạn mức</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Ví chính, ví phụ và vốn tại quỹ nhóm</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>

          <Pressable
            onPress={() => router.push('/(authenticated)/friends')}
            style={[styles.menuItem, { borderBottomColor: colors.border }]}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#2E1A47' : '#EDE9FE' }]}>
              <Ionicons name="people-outline" size={20} color={colors.violet} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Danh bạ & Bạn bè</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Kết nối bạn bè và chuyển tiền P2P nhanh</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>

          <Pressable
            onPress={() => router.push('/(authenticated)/planning')}
            style={styles.menuItem}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#382A14' : '#FEF3C7' }]}>
              <Ionicons name="pie-chart-outline" size={20} color={colors.amber} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Ngân sách & Mục tiêu tiết kiệm</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Kế hoạch chi tiêu tháng và mục tiêu</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>
        </GlassCard>

        {/* 4. SUPPORT & FAQS */}
        <Text style={[styles.sectionHeader, { color: colors.textMuted }]}>HỖ TRỢ & QUY CHẾ</Text>
        <GlassCard style={styles.menuCard}>
          <Pressable
            onPress={() => {
              Alert.alert(
                'Quy chuẩn Vận hành SpendWise OS',
                '• Không gộp chung số dư khả dụng với số dư tạm giữ hoặc vốn quỹ.\n• Quỹ cố định V2 kiểm soát ngưỡng 20% và cơ chế phản đối 24h.\n• Quỹ tự do V1 kiểm soát rút vốn theo hạn ngạch 7 ngày.\n• Mọi giao dịch sổ cái hạch toán kép toàn vẹn.'
              );
            }}
            style={[styles.menuItem, { borderBottomColor: colors.border }]}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#16382C' : '#E6F8F0' }]}>
              <Ionicons name="document-text-outline" size={20} color={colors.primary} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Quy chuẩn vận hành SpendWise</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Nguyên tắc bất biến sổ cái và quỹ nhóm</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>

          <Pressable
            onPress={() => {
              Alert.alert('Trung tâm trợ giúp', 'Đường dây nóng hỗ trợ: 1900 8888 (24/7)\nEmail: support@spendwise.vn');
            }}
            style={styles.menuItem}
          >
            <View style={[styles.menuIconBg, { backgroundColor: isDark ? '#1C382F' : '#E0F2FE' }]}>
              <Ionicons name="help-buoy-outline" size={20} color={colors.sky} />
            </View>
            <View style={styles.menuItemBody}>
              <Text style={[styles.menuItemTitle, { color: colors.text }]}>Hỗ trợ & Khiếu nại giao dịch</Text>
              <Text style={[styles.menuItemSub, { color: colors.textMuted }]}>Tiếp nhận và giải quyết khiếu nại</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
          </Pressable>
        </GlassCard>

        {/* 5. LOGOUT BUTTON */}
        <Pressable
          onPress={handleLogout}
          style={[styles.logoutBtn, { borderColor: colors.danger, backgroundColor: isDark ? '#2D1418' : '#FEE2E2' }]}
        >
          <Ionicons name="log-out-outline" size={20} color={colors.danger} />
          <Text style={[styles.logoutBtnText, { color: colors.danger }]}>Đăng xuất tài khoản</Text>
        </Pressable>

        <ProfileModal visible={showProfileModal} onClose={() => setShowProfileModal(false)} />
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.md,
    paddingBottom: 40,
  },
  profileCard: {
    padding: spacing.lg,
  },
  avatarRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
  },
  avatarContainer: {
    width: 60,
    height: 60,
    borderRadius: 30,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  avatarImage: {
    width: '100%',
    height: '100%',
  },
  profileInfo: {
    flex: 1,
    gap: 2,
  },
  userName: {
    fontSize: 18,
    fontWeight: '700',
  },
  userEmail: {
    fontSize: 13,
  },
  spendIdPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radius.pill,
    alignSelf: 'flex-start',
    marginTop: 4,
  },
  spendIdLabel: {
    fontSize: 11,
    fontWeight: '600',
  },
  spendIdVal: {
    fontSize: 12,
    fontWeight: '800',
    fontFamily: typography.brand.fontFamily,
  },
  editProfileBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sectionHeader: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 0.6,
    marginTop: 6,
    marginLeft: 4,
  },
  menuCard: {
    paddingVertical: 4,
    paddingHorizontal: spacing.md,
  },
  menuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    gap: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'transparent',
  },
  menuIconBg: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  menuItemBody: {
    flex: 1,
  },
  menuItemTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  menuItemSub: {
    fontSize: 12,
    marginTop: 2,
  },
  logoutBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 14,
    borderRadius: radius.lg,
    borderWidth: 1,
    marginTop: 8,
  },
  logoutBtnText: {
    fontSize: 15,
    fontWeight: '700',
  },
});
