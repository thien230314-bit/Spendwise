import React from 'react';
import { StyleSheet, Text, View, Pressable } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';

export interface BalanceCardProps {
  availableBalance: number;
  isBalanceHidden: boolean;
  onToggleHideBalance: () => void;
  spendId?: string;
  onOpenWallets: () => void;
  onTransfer: () => void;
  onDeposit: () => void;
  onWithdraw: () => void;
  onAddManualTx: () => void;
}

export function BalanceCard({
  availableBalance,
  isBalanceHidden,
  onToggleHideBalance,
  spendId = 'SW-9824',
  onOpenWallets,
  onTransfer,
  onDeposit,
  onWithdraw,
  onAddManualTx,
}: BalanceCardProps) {
  const { colors, isDark } = useTheme();

  const formattedBalance = isBalanceHidden
    ? '•••••••• ₫'
    : availableBalance.toLocaleString('vi-VN') + ' ₫';

  return (
    <View style={styles.wrapper}>
      {/* 1. HERO GRADIENT CARD */}
      <LinearGradient
        colors={colors.heroGradient}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={styles.heroCard}
      >
        {/* Subtle decorative glow circle */}
        <View style={styles.glowCircle} />

        {/* Header Row: Status dot + Label + Eye Toggle */}
        <View style={styles.topRow}>
          <View style={styles.statusBadge}>
            <View style={styles.greenDot} />
            <Text style={styles.heroLabel}>Số dư chi tiêu khả dụng</Text>
          </View>

          <Pressable
            accessibilityRole="button"
            accessibilityLabel={isBalanceHidden ? 'Hiện số tiền' : 'Ẩn số tiền'}
            onPress={onToggleHideBalance}
            style={({ pressed }) => [
              styles.eyeButton,
              { opacity: pressed ? 0.75 : 1 },
            ]}
          >
            <Ionicons
              name={isBalanceHidden ? 'eye-off-outline' : 'eye-outline'}
              size={18}
              color="#FFFFFF"
            />
            <Text style={styles.eyeText}>
              {isBalanceHidden ? 'Hiện' : 'Ẩn'}
            </Text>
          </Pressable>
        </View>

        {/* Hero Amount */}
        <Text
          style={styles.heroAmount}
          numberOfLines={1}
          adjustsFontSizeToFit
        >
          {formattedBalance}
        </Text>

        {/* Account Info + "Tất cả ví" Button Row */}
        <View style={styles.bottomHeroRow}>
          <View style={styles.idBadge}>
            <Text style={styles.idBadgeLabel}>Mã TK:</Text>
            <Text style={styles.idBadgeValue}>{spendId}</Text>
          </View>

          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Tất cả ví"
            onPress={onOpenWallets}
            style={({ pressed }) => [
              styles.allWalletsButton,
              { opacity: pressed ? 0.8 : 1 },
            ]}
          >
            <Text style={styles.allWalletsText}>Tất cả ví</Text>
            <Ionicons name="chevron-forward" size={16} color="#FFFFFF" />
          </Pressable>
        </View>
      </LinearGradient>

      {/* 2. 2x2 PRIMARY ACTION GRID */}
      <View style={styles.actionGrid}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Chuyển tiền"
          onPress={onTransfer}
          style={({ pressed }) => [
            styles.actionButton,
            {
              backgroundColor: isDark ? colors.surfaceElevated : '#FFFFFF',
              borderColor: colors.border,
              opacity: pressed ? 0.85 : 1,
            },
          ]}
        >
          <View style={[styles.actionIconBg, { backgroundColor: isDark ? '#16382C' : '#E6F9F0' }]}>
            <Ionicons name="paper-plane" size={20} color={colors.primary} />
          </View>
          <Text style={[styles.actionLabel, { color: colors.text }]}>Chuyển tiền</Text>
        </Pressable>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Ghi thu chi"
          onPress={onAddManualTx}
          style={({ pressed }) => [
            styles.actionButton,
            {
              backgroundColor: isDark ? colors.surfaceElevated : '#FFFFFF',
              borderColor: colors.border,
              opacity: pressed ? 0.85 : 1,
            },
          ]}
        >
          <View style={[styles.actionIconBg, { backgroundColor: isDark ? '#16382C' : '#E6F9F0' }]}>
            <Ionicons name="create-outline" size={20} color={colors.primary} />
          </View>
          <Text style={[styles.actionLabel, { color: colors.text }]}>Ghi thu chi</Text>
        </Pressable>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Nạp tiền"
          onPress={onDeposit}
          style={({ pressed }) => [
            styles.actionButton,
            {
              backgroundColor: isDark ? colors.surfaceElevated : '#FFFFFF',
              borderColor: colors.border,
              opacity: pressed ? 0.85 : 1,
            },
          ]}
        >
          <View style={[styles.actionIconBg, { backgroundColor: isDark ? '#16382C' : '#E6F9F0' }]}>
            <Ionicons name="arrow-down" size={20} color={colors.primary} />
          </View>
          <Text style={[styles.actionLabel, { color: colors.text }]}>Nạp tiền</Text>
        </Pressable>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Rút tiền"
          onPress={onWithdraw}
          style={({ pressed }) => [
            styles.actionButton,
            {
              backgroundColor: isDark ? colors.surfaceElevated : '#FFFFFF',
              borderColor: colors.border,
              opacity: pressed ? 0.85 : 1,
            },
          ]}
        >
          <View style={[styles.actionIconBg, { backgroundColor: isDark ? '#16382C' : '#E6F9F0' }]}>
            <Ionicons name="arrow-up" size={20} color={colors.primary} />
          </View>
          <Text style={[styles.actionLabel, { color: colors.text }]}>Rút tiền</Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    marginVertical: spacing.sm,
  },
  heroCard: {
    borderRadius: radius.hero,
    padding: spacing.lg,
    overflow: 'hidden',
    position: 'relative',
    shadowColor: '#0b6e50',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.25,
    shadowRadius: 18,
    elevation: 6,
  },
  glowCircle: {
    position: 'absolute',
    top: -50,
    right: -50,
    width: 160,
    height: 160,
    borderRadius: 80,
    backgroundColor: 'rgba(255, 255, 255, 0.12)',
  },
  topRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  greenDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#34D399',
    shadowColor: '#34D399',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.9,
    shadowRadius: 4,
  },
  heroLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: 'rgba(255, 255, 255, 0.92)',
  },
  eyeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(255, 255, 255, 0.18)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
  },
  eyeText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  heroAmount: {
    fontSize: 34,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: -0.6,
    marginVertical: 14,
  },
  bottomHeroRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  idBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: 'rgba(255, 255, 255, 0.22)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.md,
  },
  idBadgeLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: 'rgba(255, 255, 255, 0.85)',
  },
  idBadgeValue: {
    fontSize: 12,
    fontWeight: '800',
    color: '#FFFFFF',
  },
  allWalletsButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
  },
  allWalletsText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  actionGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: spacing.md,
    gap: 10,
  },
  actionButton: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: radius.lg,
    borderWidth: 1,
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 6,
    elevation: 2,
  },
  actionIconBg: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 6,
  },
  actionLabel: {
    fontSize: 12,
    fontWeight: '700',
  },
});
