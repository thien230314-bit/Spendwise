import React, { useState } from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SymbolView } from 'expo-symbols';
import { AppScreen } from '@/components/AppScreen';
import { Badge, Card, Copy, SectionTitle, UIIcon } from '@/components/MobileUI';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function WalletScreen() {
  const { colors, isDark } = useTheme();
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<'all' | 'income' | 'expense'>('all');

  const transactions = [
    {
      id: 'tx-1',
      title: 'Siêu thị WinMart',
      category: 'Ăn uống',
      amount: -245000,
      type: 'expense' as const,
      date: 'Hôm nay, 14:20',
    },
    {
      id: 'tx-2',
      title: 'Lương tháng 10 TechCorp',
      category: 'Thu nhập',
      amount: 28500000,
      type: 'income' as const,
      date: 'Hôm qua',
    },
    {
      id: 'tx-3',
      title: 'Xăng xe Petrolimex',
      category: 'Đi lại',
      amount: -110000,
      type: 'expense' as const,
      date: '11/10/2026',
    },
    {
      id: 'tx-4',
      title: 'Chuyển tiền tiết kiệm',
      category: 'Tích lũy',
      amount: -3000000,
      type: 'expense' as const,
      date: '10/10/2026',
    },
  ];

  const filteredList = transactions.filter((tx) => {
    const matchFilter = filter === 'all' || tx.type === filter;
    const matchQuery =
      !query.trim() ||
      tx.title.toLowerCase().includes(query.toLowerCase()) ||
      tx.category.toLowerCase().includes(query.toLowerCase());
    return matchFilter && matchQuery;
  });

  return (
    <AppScreen title="Ví & Giao dịch" subtitle="Quản lý ví SpendWise và lịch sử thu chi.">
      <View style={styles.container}>
        {/* 1. Primary Wallet Card (Obsidian Emerald Theme matching Stitch V2) */}
        <View
          style={[
            styles.walletCard,
            {
              backgroundColor: '#064E3B',
              borderColor: '#047857',
            },
          ]}
        >
          <View style={styles.cardHeaderRow}>
            <View style={styles.cardBadgeWrap}>
              <View style={[styles.dotIndicator, { backgroundColor: '#34D399' }]} />
              <Text style={[styles.cardHeaderTitle, { color: '#FFFFFF' }]}>
                Ví SpendWise
              </Text>
              <View style={[styles.tagBadge, { backgroundColor: 'rgba(52, 211, 153, 0.2)' }]}>
                <Text style={[styles.tagBadgeText, { color: '#A7F3D0' }]}>
                  Nội bộ
                </Text>
              </View>
            </View>

            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Tạo ví mới"
              style={({ pressed }) => [{ opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.createLinkText, { color: '#6EE7B7' }]}>
                + Tạo ví mới
              </Text>
            </Pressable>
          </View>

          {/* Wallet selector box */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Chọn ví nguồn"
            style={({ pressed }) => [
              styles.walletPickerBox,
              {
                backgroundColor: 'rgba(0, 0, 0, 0.25)',
                borderColor: 'rgba(52, 211, 153, 0.3)',
                opacity: pressed ? 0.8 : 1,
              },
            ]}
          >
            <View style={styles.walletPickerTextWrap}>
              <Text style={[styles.walletNameText, { color: '#FFFFFF' }]}>
                Ví Chi tiêu Cá nhân (Mặc định)
              </Text>
              <Text style={[styles.walletSubText, { color: '#A7F3D0' }]}>
                Mã ví: SW-98234-01 • Đang hoạt động
              </Text>
            </View>
            <SymbolView
              name={{ ios: 'chevron.down', android: 'expand_more', web: 'expand_more' }}
              size={18}
              tintColor="#A7F3D0"
            />
          </Pressable>

          <View style={styles.balanceWrap}>
            <Text style={[styles.balanceSubhead, { color: '#A7F3D0' }]}>
              Số dư khả dụng
            </Text>
            <Text style={[styles.balanceBigNumber, { color: '#FFFFFF' }]}>
              14.250.000 ₫
            </Text>
          </View>
        </View>

        {/* 2. Linked Bank Account Card (SePay) */}
        <View
          style={[
            styles.bankCard,
            {
              backgroundColor: isDark ? '#101622' : '#FFFFFF',
              borderColor: colors.border,
            },
          ]}
        >
          <View style={styles.cardHeaderRow}>
            <View style={styles.cardBadgeWrap}>
              <SymbolView
                name={{ ios: 'building.columns.fill', android: 'account_balance', web: 'account_balance' }}
                size={16}
                tintColor={colors.primary}
              />
              <Text style={[styles.cardHeaderTitle, { color: colors.text }]}>
                Ngân hàng liên kết
              </Text>
              <View style={[styles.tagBadge, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
                <Text style={[styles.tagBadgeText, { color: colors.textMuted }]}>
                  SePay
                </Text>
              </View>
            </View>

            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Liên kết thêm ngân hàng"
              style={({ pressed }) => [{ opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.createLinkText, { color: colors.primary }]}>
                + Liên kết
              </Text>
            </Pressable>
          </View>

          <View
            style={[
              styles.bankAccountRow,
              {
                backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                borderColor: colors.border,
              },
            ]}
          >
            <View style={styles.bankLogoCircle}>
              <Text style={styles.bankLogoText}>VCB</Text>
            </View>
            <View style={styles.bankDetails}>
              <Text style={[styles.bankNameText, { color: colors.text }]}>
                Vietcombank • 9832
              </Text>
              <Text style={[styles.bankHolderText, { color: colors.textMuted }]}>
                Nguyen Van An • Tài khoản chính
              </Text>
            </View>
            <View style={[styles.connectedPill, { backgroundColor: colors.primarySoft }]}>
              <Text style={[styles.connectedPillText, { color: colors.primary }]}>
                Đã kết nối
              </Text>
            </View>
          </View>
        </View>

        {/* 3. Search & Filter Bar (Inspired by Reference Images 1 & 2) */}
        <View style={styles.searchRow}>
          <View
            style={[
              styles.searchBox,
              {
                backgroundColor: isDark ? '#131A26' : '#FFFFFF',
                borderColor: colors.border,
              },
            ]}
          >
            <SymbolView
              name={{ ios: 'magnifyingglass', android: 'search', web: 'search' }}
              size={18}
              tintColor={colors.textMuted}
            />
            <TextInput
              placeholder="Tìm kiếm giao dịch, danh mục…"
              placeholderTextColor={colors.textMuted}
              value={query}
              onChangeText={setQuery}
              style={[styles.searchInput, { color: colors.text }]}
            />
            {query ? (
              <Pressable onPress={() => setQuery('')} hitSlop={8}>
                <SymbolView
                  name={{ ios: 'xmark.circle.fill', android: 'cancel', web: 'cancel' }}
                  size={16}
                  tintColor={colors.textMuted}
                />
              </Pressable>
            ) : null}
          </View>
        </View>

        {/* Segmented Filter Pills */}
        <View
          style={[
            styles.segmentBar,
            {
              backgroundColor: isDark ? '#101622' : '#F1F5F9',
              borderColor: colors.border,
            },
          ]}
        >
          {(['all', 'income', 'expense'] as const).map((key) => {
            const labels = { all: 'Tất cả', income: 'Thu nhập', expense: 'Chi tiêu' };
            const isActive = filter === key;
            return (
              <Pressable
                key={key}
                onPress={() => setFilter(key)}
                style={[
                  styles.segmentBtn,
                  isActive && {
                    backgroundColor: isDark ? '#1E293B' : '#FFFFFF',
                    shadowColor: '#000000',
                    shadowOffset: { width: 0, height: 1 },
                    shadowOpacity: 0.05,
                    shadowRadius: 2,
                    elevation: 1,
                  },
                ]}
              >
                <Text
                  style={[
                    styles.segmentText,
                    {
                      color: isActive ? (isDark ? '#34D399' : '#047857') : colors.textMuted,
                      fontWeight: isActive ? '700' : '500',
                    },
                  ]}
                >
                  {labels[key]}
                </Text>
              </Pressable>
            );
          })}
        </View>

        {/* 4. Transactions List Card */}
        <View
          style={[
            styles.txListCard,
            {
              backgroundColor: isDark ? '#101622' : '#FFFFFF',
              borderColor: colors.border,
            },
          ]}
        >
          <View style={styles.sectionHeadRow}>
            <Text style={[styles.sectionTitle, { color: colors.text }]}>
              Lịch sử giao dịch ({filter === 'all' ? 'Tất cả' : filter === 'income' ? 'Thu nhập' : 'Chi tiêu'})
            </Text>
          </View>

          <View style={styles.txListContainer}>
            {filteredList.map((tx) => (
              <View
                key={tx.id}
                style={[
                  styles.txRow,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View
                  style={[
                    styles.txIconCircle,
                    {
                      backgroundColor:
                        tx.type === 'income'
                          ? 'rgba(21, 128, 61, 0.12)'
                          : 'rgba(220, 38, 38, 0.12)',
                    },
                  ]}
                >
                  <UIIcon
                    name={tx.type === 'income' ? 'income' : 'expense'}
                    size={16}
                    color={tx.type === 'income' ? colors.success : colors.danger}
                  />
                </View>
                <View style={styles.txMeta}>
                  <Text style={[styles.txTitleText, { color: colors.text }]}>
                    {tx.title}
                  </Text>
                  <Text style={[styles.txDateText, { color: colors.textMuted }]}>
                    {tx.category} • {tx.date}
                  </Text>
                </View>
                <Text
                  style={[
                    styles.txAmountText,
                    { color: tx.type === 'income' ? colors.success : colors.text },
                  ]}
                >
                  {tx.type === 'income' ? '+' : '−'}
                  {Math.abs(tx.amount).toLocaleString('vi-VN')} ₫
                </Text>
              </View>
            ))}
          </View>
        </View>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    paddingBottom: spacing.xxl,
  },
  walletCard: {
    borderRadius: 22,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.md,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 4,
    elevation: 1,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  cardBadgeWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  dotIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  cardHeaderTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  tagBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 12,
  },
  tagBadgeText: {
    fontSize: 10,
    fontWeight: '700',
  },
  createLinkText: {
    fontSize: 12,
    fontWeight: '600',
  },
  walletPickerBox: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: spacing.sm + 2,
    borderRadius: 16,
    borderWidth: 1,
  },
  walletPickerTextWrap: {
    gap: 2,
  },
  walletNameText: {
    fontSize: 13,
    fontWeight: '700',
  },
  walletSubText: {
    fontSize: 11,
  },
  balanceWrap: {
    gap: 2,
    paddingTop: 2,
  },
  balanceSubhead: {
    fontSize: 11,
    fontWeight: '500',
  },
  balanceBigNumber: {
    fontSize: 28,
    lineHeight: 36,
    fontWeight: '800',
    fontFamily: 'monospace',
    letterSpacing: -0.5,
  },
  bankCard: {
    borderRadius: 22,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.md,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 4,
    elevation: 1,
  },
  bankAccountRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: 16,
    borderWidth: 1,
  },
  bankLogoCircle: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: '#0F172A',
    alignItems: 'center',
    justifyContent: 'center',
  },
  bankLogoText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
  },
  bankDetails: {
    flex: 1,
    gap: 2,
  },
  bankNameText: {
    fontSize: 13,
    fontWeight: '600',
  },
  bankHolderText: {
    fontSize: 11,
  },
  connectedPill: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 10,
  },
  connectedPillText: {
    fontSize: 10,
    fontWeight: '700',
  },
  searchRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  searchBox: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 18,
    paddingHorizontal: spacing.md,
    height: 46,
    gap: spacing.sm,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.03,
    shadowRadius: 3,
    elevation: 1,
  },
  searchInput: {
    flex: 1,
    fontSize: 13,
    paddingVertical: 0,
  },
  segmentBar: {
    flexDirection: 'row',
    padding: 3,
    borderRadius: 14,
    borderWidth: 1,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 11,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentText: {
    fontSize: 12,
  },
  txListCard: {
    borderRadius: 22,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.md,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 4,
    elevation: 1,
  },
  sectionHeadRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: -0.2,
  },
  txListContainer: {
    gap: spacing.xs + 2,
  },
  txRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: 14,
    borderWidth: 1,
  },
  txIconCircle: {
    width: 36,
    height: 36,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  txMeta: {
    flex: 1,
    gap: 2,
  },
  txTitleText: {
    fontSize: 13,
    fontWeight: '600',
  },
  txDateText: {
    fontSize: 11,
  },
  txAmountText: {
    fontSize: 13,
    fontWeight: '700',
    fontFamily: 'monospace',
  },
});
