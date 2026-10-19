import React, { useState } from 'react';
import { router } from 'expo-router';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SymbolView } from 'expo-symbols';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { UIIcon } from '@/components/MobileUI';
import { LogoutButton } from '@/components/LogoutButton';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function HomeScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();

  // State
  const [isBalanceHidden, setIsBalanceHidden] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [chartViewMode, setChartViewMode] = useState<'all' | 'income' | 'expense'>('all');
  const [donutMode, setDonutMode] = useState<'expense' | 'income'>('expense');

  // Realistic mock data matching SpendWise User Web contracts & Backend DTOs
  const totalBalance = 14250000;
  const monthIncome = 42500000;
  const monthExpense = 18250000;
  const budgetRemaining = 6750000;
  const netCashFlow = monthIncome - monthExpense; // +24.250.000 ₫

  const formattedBalance = isBalanceHidden
    ? '•••••••• ₫'
    : totalBalance.toLocaleString('vi-VN') + ' ₫';

  // 6-month historical flow data (Web Parity)
  const monthlyBarData = [
    { month: 'T5', income: 38000000, expense: 21000000 },
    { month: 'T6', income: 41000000, expense: 24500000 },
    { month: 'T7', income: 39500000, expense: 22800000 },
    { month: 'T8', income: 43000000, expense: 26000000 },
    { month: 'T9', income: 44500000, expense: 29550000 },
    { month: 'T10', income: 42500000, expense: 18250000 },
  ];
  const maxBarValue = 50000000;
  const total6MIncome = 248500000;
  const total6MExpense = 142100000;

  // Donut category breakdown
  const categorySlices = [
    { name: 'Ăn uống & Nhà hàng', percentage: 42, amount: 7665000, color: '#047857' },
    { name: 'Mua sắm & Tiêu dùng', percentage: 28, amount: 5110000, color: '#10B981' },
    { name: 'Nhà cửa & Hóa đơn', percentage: 18, amount: 3285000, color: '#6366F1' },
    { name: 'Khác & Dự phòng', percentage: 12, amount: 2190000, color: '#F59E0B' },
  ];

  // Financial Attention alerts
  const attentionItems = [
    {
      id: 'att-1',
      title: 'Ngân sách Ăn uống đã chạm 84% hạn mức',
      description: 'Còn lại 1.260.000 ₫ cho 12 ngày còn lại của tháng.',
      actionLabel: 'Xem chi tiết',
      priority: 'high',
      route: '/(authenticated)/planning',
    },
    {
      id: 'att-2',
      title: 'Quỹ Du lịch Đà Nẵng: Đến hạn đóng kỳ 3',
      description: 'Số tiền cần đóng: 1.500.000 ₫ (Hạn chót: 20/10)',
      actionLabel: 'Đóng ngay',
      priority: 'urgent',
      route: '/(authenticated)/friends',
    },
  ];

  // Group funds
  const groupFunds = [
    {
      id: 'grp-1',
      name: 'Quỹ Du lịch Đà Nẵng 2026',
      membersCount: 5,
      cycle: 'Chu kỳ hàng tháng',
      balance: 18500000,
      obligation: 1500000,
      status: 'Đã hoàn thành 2/3 kỳ',
    },
    {
      id: 'grp-2',
      name: 'Quỹ Ăn trưa Công ty',
      membersCount: 8,
      cycle: 'Đóng góp linh hoạt',
      balance: 3200000,
      obligation: null,
      status: 'Đang hoạt động',
    },
  ];

  // Savings goals
  const savingsGoals = [
    {
      id: 'goal-1',
      name: 'Quỹ khẩn cấp 6 tháng',
      current: 36000000,
      target: 50000000,
      progress: 72,
    },
    {
      id: 'goal-2',
      name: 'Mua Macbook Pro M3',
      current: 21000000,
      target: 35000000,
      progress: 60,
    },
  ];

  // Exactly 5 Recent Transactions matching Stitch V2
  const recentTransactions = [
    {
      id: 'tx-1',
      title: 'Nhà hàng Maison Marou',
      category: 'Ăn uống',
      amount: -350000,
      time: 'Hôm nay, 12:45',
      type: 'expense' as const,
      iconName: 'fork.knife',
      iconAndroid: 'restaurant',
    },
    {
      id: 'tx-2',
      title: 'Nhận lương tháng 10',
      category: 'Thu nhập',
      amount: 25000000,
      time: 'Hôm qua, 09:00',
      type: 'income' as const,
      iconName: 'banknote',
      iconAndroid: 'payments',
    },
    {
      id: 'tx-3',
      title: 'Starbucks Coffee',
      category: 'Ăn uống',
      amount: -115000,
      time: '14 Tháng 10',
      type: 'expense' as const,
      iconName: 'cup.and.saucer',
      iconAndroid: 'coffee',
    },
    {
      id: 'tx-4',
      title: 'Chuyển tiền đóng Quỹ nhóm Đà Nẵng',
      category: 'Quỹ nhóm',
      amount: -1500000,
      time: '13 Tháng 10',
      type: 'expense' as const,
      iconName: 'person.2',
      iconAndroid: 'groups',
    },
    {
      id: 'tx-5',
      title: 'Thanh toán Shopee Express',
      category: 'Mua sắm',
      amount: -420000,
      time: '12 Tháng 10',
      type: 'expense' as const,
      iconName: 'cart',
      iconAndroid: 'shopping_bag',
    },
  ];

  return (
    <AppScreen title="Trang chủ" subtitle="Tình hình tài chính của bạn hôm nay.">
      <View style={styles.container}>
        {/* ========================================================
            BLOCK 01: HEADER + DEDICATED SEARCH BAR
            ======================================================== */}
        <View style={styles.headerBlock}>
          <View style={styles.topBarRow}>
            <View style={styles.userProfileBlock}>
              <View
                style={[
                  styles.avatarCircle,
                  {
                    backgroundColor: colors.primarySoft,
                    borderColor: isDark ? 'rgba(52, 211, 153, 0.25)' : 'rgba(4, 120, 87, 0.15)',
                  },
                ]}
              >
                <UIIcon name="profile" size={26} color={colors.primary} />
                <View style={styles.onlineDot} />
              </View>
              <View style={styles.userTextBlock}>
                <Text style={[styles.welcomeGreeting, { color: colors.textMuted }]}>
                  Xin chào,
                </Text>
                <Text style={[styles.welcomeName, { color: colors.text }]}>
                  {user?.fullName || 'Lan Anh'}
                </Text>
              </View>
            </View>

            <View style={styles.topRightActions}>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Thông báo"
                style={({ pressed }) => [
                  styles.headerIconBtn,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#FFFFFF',
                    borderColor: colors.border,
                    opacity: pressed ? 0.7 : 1,
                  },
                ]}
              >
                <SymbolView
                  name={{ ios: 'bell.fill', android: 'notifications', web: 'notifications' }}
                  size={18}
                  tintColor={colors.text}
                />
                <View style={styles.notifBadgeDot} />
              </Pressable>
            </View>
          </View>

          {/* Dedicated Search Bar (18px radius pill with dark filter button) */}
          <View style={styles.searchSectionRow}>
            <View
              style={[
                styles.premiumSearchBox,
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
                placeholder="Tìm bạn bè, ví, giao dịch..."
                placeholderTextColor={colors.textMuted}
                value={searchQuery}
                onChangeText={setSearchQuery}
                style={[styles.searchInputText, { color: colors.text }]}
              />
              {searchQuery ? (
                <Pressable onPress={() => setSearchQuery('')} hitSlop={8}>
                  <SymbolView
                    name={{ ios: 'xmark.circle.fill', android: 'cancel', web: 'cancel' }}
                    size={16}
                    tintColor={colors.textMuted}
                  />
                </Pressable>
              ) : null}
            </View>

            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Bộ lọc"
              style={({ pressed }) => [
                styles.filterPillBtn,
                {
                  backgroundColor: isDark ? '#1E293B' : '#0F172A',
                  opacity: pressed ? 0.8 : 1,
                },
              ]}
            >
              <SymbolView
                name={{ ios: 'slider.horizontal.3', android: 'tune', web: 'tune' }}
                size={16}
                tintColor="#FFFFFF"
              />
            </Pressable>
          </View>
        </View>

        {/* ========================================================
            BLOCK 02: WALLET BALANCE (OBSIDIAN-EMERALD CARD #064E3B)
            ======================================================== */}
        <View style={styles.obsidianCard}>
          {/* Card Top Label & Quick Eye Toggle */}
          <View style={styles.cardHeaderRow}>
            <View style={styles.walletBadgeWrap}>
              <SymbolView
                name={{ ios: 'wallet.bifold.fill', android: 'account_balance_wallet', web: 'account_balance_wallet' }}
                size={16}
                tintColor="#A7F3D0"
              />
              <Text style={styles.walletBadgeLabel}>
                TỔNG TÀI SẢN RÒNG
              </Text>
            </View>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={isBalanceHidden ? 'Hiện số dư' : 'Ẩn số dư'}
              onPress={() => setIsBalanceHidden(!isBalanceHidden)}
              style={({ pressed }) => [styles.eyeButton, { opacity: pressed ? 0.6 : 1 }]}
            >
              <SymbolView
                name={{
                  ios: isBalanceHidden ? 'eye.slash.fill' : 'eye.fill',
                  android: isBalanceHidden ? 'visibility_off' : 'visibility',
                  web: isBalanceHidden ? 'visibility_off' : 'visibility',
                }}
                size={18}
                tintColor="#E2E8F0"
              />
            </Pressable>
          </View>

          {/* Large Readable Balance */}
          <View style={styles.balanceContainer}>
            <Text
              numberOfLines={1}
              adjustsFontSizeToFit
              minimumFontScale={0.75}
              style={styles.balanceNumber}
            >
              {formattedBalance}
            </Text>
          </View>

          {/* Card Bottom Meta */}
          <View style={styles.cardBottomRow}>
            <View style={styles.accumulationPill}>
              <View style={styles.accumulationDot} />
              <Text style={styles.accumulationText}>
                Tỷ lệ tích lũy: 42%
              </Text>
            </View>
            <Text style={styles.cardSafetyStatus}>
              An toàn tài chính
            </Text>
          </View>
        </View>

        {/* ========================================================
            BLOCK 03: FOUR QUICK ACTIONS (Thu nhập, Chi tiêu, Chuyển, Quỹ nhóm)
            ======================================================== */}
        <View style={styles.quickActionsRow}>
          {/* 1. Thu nhập */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Thu nhập"
            onPress={() => router.navigate('/(authenticated)/wallet')}
            style={({ pressed }) => [styles.quickActionItem, { opacity: pressed ? 0.7 : 1 }]}
          >
            <View style={[styles.quickActionIconBox, { backgroundColor: isDark ? 'rgba(16, 185, 129, 0.15)' : '#ECFDF5', borderColor: 'rgba(16, 185, 129, 0.25)' }]}>
              <SymbolView
                name={{ ios: 'arrow.down.left.circle.fill', android: 'add_circle', web: 'add_circle' }}
                size={22}
                tintColor="#047857"
              />
            </View>
            <Text style={[styles.quickActionLabel, { color: colors.text }]}>
              Thu nhập
            </Text>
          </Pressable>

          {/* 2. Chi tiêu */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Chi tiêu"
            onPress={() => router.navigate('/(authenticated)/wallet')}
            style={({ pressed }) => [styles.quickActionItem, { opacity: pressed ? 0.7 : 1 }]}
          >
            <View style={[styles.quickActionIconBox, { backgroundColor: isDark ? 'rgba(239, 68, 68, 0.15)' : '#FEF2F2', borderColor: 'rgba(239, 68, 68, 0.25)' }]}>
              <SymbolView
                name={{ ios: 'arrow.up.right.circle.fill', android: 'remove_circle', web: 'remove_circle' }}
                size={22}
                tintColor="#DC2626"
              />
            </View>
            <Text style={[styles.quickActionLabel, { color: colors.text }]}>
              Chi tiêu
            </Text>
          </Pressable>

          {/* 3. Chuyển tiền */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Chuyển tiền"
            onPress={() => router.navigate('/(authenticated)/transfer')}
            style={({ pressed }) => [styles.quickActionItem, { opacity: pressed ? 0.7 : 1 }]}
          >
            <View style={[styles.quickActionIconBox, { backgroundColor: isDark ? 'rgba(6, 182, 212, 0.15)' : '#EFF6FF', borderColor: 'rgba(6, 182, 212, 0.25)' }]}>
              <SymbolView
                name={{ ios: 'paperplane.fill', android: 'send', web: 'send' }}
                size={20}
                tintColor="#0284C7"
              />
            </View>
            <Text style={[styles.quickActionLabel, { color: colors.text }]}>
              Chuyển tiền
            </Text>
          </Pressable>

          {/* 4. Quỹ nhóm */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Quỹ nhóm"
            onPress={() => router.navigate('/(authenticated)/friends')}
            style={({ pressed }) => [styles.quickActionItem, { opacity: pressed ? 0.7 : 1 }]}
          >
            <View style={[styles.quickActionIconBox, { backgroundColor: isDark ? 'rgba(99, 102, 241, 0.15)' : '#EEF2FF', borderColor: 'rgba(99, 102, 241, 0.25)' }]}>
              <SymbolView
                name={{ ios: 'person.2.fill', android: 'group', web: 'group' }}
                size={22}
                tintColor="#4F46E5"
              />
            </View>
            <Text style={[styles.quickActionLabel, { color: colors.text }]}>
              Quỹ nhóm
            </Text>
          </Pressable>
        </View>

        {/* ========================================================
            BLOCK 04: COMPACT FINANCIAL OVERVIEW (2x2 KPI GRID)
            ======================================================== */}
        <View style={styles.kpiGridContainer}>
          {/* Card 1: Thu tháng này */}
          <View style={[styles.kpiCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
            <View style={styles.kpiHeaderRow}>
              <Text style={[styles.kpiLabel, { color: colors.textMuted }]}>Thu tháng này</Text>
              <View style={[styles.kpiIconDot, { backgroundColor: 'rgba(16, 185, 129, 0.15)' }]}>
                <SymbolView
                  name={{ ios: 'arrow.down.left', android: 'south_west', web: 'south_west' }}
                  size={12}
                  tintColor="#047857"
                />
              </View>
            </View>
            <Text style={[styles.kpiValue, { color: colors.success }]}>
              +{monthIncome.toLocaleString('vi-VN')} ₫
            </Text>
          </View>

          {/* Card 2: Chi tháng này */}
          <View style={[styles.kpiCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
            <View style={styles.kpiHeaderRow}>
              <Text style={[styles.kpiLabel, { color: colors.textMuted }]}>Chi tháng này</Text>
              <View style={[styles.kpiIconDot, { backgroundColor: 'rgba(239, 68, 68, 0.15)' }]}>
                <SymbolView
                  name={{ ios: 'arrow.up.right', android: 'north_east', web: 'north_east' }}
                  size={12}
                  tintColor="#DC2626"
                />
              </View>
            </View>
            <Text style={[styles.kpiValue, { color: colors.danger }]}>
              −{monthExpense.toLocaleString('vi-VN')} ₫
            </Text>
          </View>

          {/* Card 3: Ngân sách còn lại */}
          <View style={[styles.kpiCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
            <View style={styles.kpiHeaderRow}>
              <Text style={[styles.kpiLabel, { color: colors.textMuted }]}>Ngân sách còn lại</Text>
              <View style={[styles.kpiIconDot, { backgroundColor: 'rgba(6, 182, 212, 0.15)' }]}>
                <SymbolView
                  name={{ ios: 'chart.pie.fill', android: 'pie_chart', web: 'pie_chart' }}
                  size={12}
                  tintColor="#0284C7"
                />
              </View>
            </View>
            <Text style={[styles.kpiValue, { color: colors.text }]}>
              {budgetRemaining.toLocaleString('vi-VN')} ₫
            </Text>
          </View>

          {/* Card 4: Dòng tiền ròng (NỔI BẬT) */}
          <View
            style={[
              styles.kpiCard,
              {
                backgroundColor: isDark ? '#06261E' : '#ECFDF5',
                borderColor: isDark ? 'rgba(52, 211, 153, 0.3)' : 'rgba(16, 185, 129, 0.3)',
              },
            ]}
          >
            <View style={styles.kpiHeaderRow}>
              <Text style={[styles.kpiLabel, { color: '#047857', fontWeight: '700' }]}>
                Dòng tiền ròng
              </Text>
              <View style={[styles.kpiIconDot, { backgroundColor: 'rgba(4, 120, 87, 0.2)' }]}>
                <SymbolView
                  name={{ ios: 'chart.line.uptrend.xyaxis', android: 'trending_up', web: 'trending_up' }}
                  size={12}
                  tintColor="#047857"
                />
              </View>
            </View>
            <Text style={[styles.kpiValue, { color: '#047857', fontWeight: '800' }]}>
              +{netCashFlow.toLocaleString('vi-VN')} ₫
            </Text>
          </View>
        </View>

        {/* ========================================================
            BLOCK 05: SIX-MONTH CASH FLOW BAR CHART
            ======================================================== */}
        <View style={[styles.contentCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
          <View style={styles.sectionHeaderRow}>
            <View>
              <Text style={[styles.sectionTitleText, { color: colors.text }]}>
                Biến động 6 tháng
              </Text>
              <Text style={[styles.sectionSubText, { color: colors.textMuted }]}>
                Tháng 5 - Tháng 10
              </Text>
            </View>

            {/* Segmented Mode Selector: Tất cả / Chỉ Thu / Chỉ Chi */}
            <View style={[styles.chartModeToggle, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
              {(['all', 'income', 'expense'] as const).map((mode) => (
                <Pressable
                  key={mode}
                  onPress={() => setChartViewMode(mode)}
                  style={[
                    styles.chartModeBtn,
                    chartViewMode === mode && {
                      backgroundColor: isDark ? '#0F1422' : '#FFFFFF',
                      shadowColor: '#000000',
                      shadowOffset: { width: 0, height: 1 },
                      shadowOpacity: 0.1,
                      shadowRadius: 2,
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.chartModeText,
                      {
                        color:
                          chartViewMode === mode
                            ? colors.primary
                            : colors.textMuted,
                        fontWeight: chartViewMode === mode ? '700' : '500',
                      },
                    ]}
                  >
                    {mode === 'all' ? 'Tất cả' : mode === 'income' ? 'Chỉ Thu' : 'Chỉ Chi'}
                  </Text>
                </Pressable>
              ))}
            </View>
          </View>

          {/* Bar Chart Container */}
          <View style={styles.chartContainer}>
            {monthlyBarData.map((item, idx) => {
              const incomeH = Math.round((item.income / maxBarValue) * 85);
              const expenseH = Math.round((item.expense / maxBarValue) * 85);
              return (
                <View key={idx} style={styles.chartCol}>
                  <View style={styles.barsGroup}>
                    {(chartViewMode === 'all' || chartViewMode === 'income') && (
                      <View
                        style={[
                          styles.barItem,
                          {
                            backgroundColor: colors.success,
                            height: incomeH,
                          },
                        ]}
                      />
                    )}
                    {(chartViewMode === 'all' || chartViewMode === 'expense') && (
                      <View
                        style={[
                          styles.barItem,
                          {
                            backgroundColor: '#F87171',
                            height: expenseH,
                          },
                        ]}
                      />
                    )}
                  </View>
                  <Text style={[styles.chartMonthLabel, { color: colors.textMuted }]}>
                    {item.month}
                  </Text>
                </View>
              );
            })}
          </View>

          {/* Summary Row under chart */}
          <View style={[styles.chartSummaryRow, { borderTopColor: colors.border }]}>
            <View style={styles.chartSummaryItem}>
              <View style={[styles.legendDot, { backgroundColor: colors.success }]} />
              <Text style={[styles.chartSummaryLabel, { color: colors.textMuted }]}>
                Tổng Thu:
              </Text>
              <Text style={[styles.chartSummaryAmount, { color: colors.success }]}>
                {total6MIncome.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
            <View style={styles.chartSummaryItem}>
              <View style={[styles.legendDot, { backgroundColor: '#F87171' }]} />
              <Text style={[styles.chartSummaryLabel, { color: colors.textMuted }]}>
                Tổng Chi:
              </Text>
              <Text style={[styles.chartSummaryAmount, { color: colors.danger }]}>
                {total6MExpense.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
          </View>
        </View>

        {/* ========================================================
            BLOCK 06: CATEGORY DISTRIBUTION DONUT
            ======================================================== */}
        <View style={[styles.contentCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
          <View style={styles.sectionHeaderRow}>
            <View>
              <Text style={[styles.sectionTitleText, { color: colors.text }]}>
                Phân bổ chi tiêu tháng 10
              </Text>
              <Text style={[styles.sectionSubText, { color: colors.textMuted }]}>
                Cơ cấu dòng tiền theo nhóm
              </Text>
            </View>

            {/* Segmented control: Chi tiêu vs Thu nhập */}
            <View style={[styles.flowToggle, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
              <Pressable
                onPress={() => setDonutMode('expense')}
                style={[
                  styles.flowButton,
                  donutMode === 'expense' && {
                    backgroundColor: isDark ? '#0F1422' : '#FFFFFF',
                  },
                ]}
              >
                <Text
                  style={[
                    styles.flowText,
                    {
                      color: donutMode === 'expense' ? colors.danger : colors.textMuted,
                      fontWeight: donutMode === 'expense' ? '700' : '500',
                    },
                  ]}
                >
                  Chi tiêu
                </Text>
              </Pressable>
              <Pressable
                onPress={() => setDonutMode('income')}
                style={[
                  styles.flowButton,
                  donutMode === 'income' && {
                    backgroundColor: isDark ? '#0F1422' : '#FFFFFF',
                  },
                ]}
              >
                <Text
                  style={[
                    styles.flowText,
                    {
                      color: donutMode === 'income' ? colors.success : colors.textMuted,
                      fontWeight: donutMode === 'income' ? '700' : '500',
                    },
                  ]}
                >
                  Thu nhập
                </Text>
              </Pressable>
            </View>
          </View>

          {/* Central Donut Graphic Preview */}
          <View style={styles.donutGraphicBox}>
            <View style={[styles.donutOuterRing, { borderColor: isDark ? '#1E293B' : '#E2E8F0' }]}>
              <View style={[styles.donutSegment1, { borderColor: '#047857' }]} />
              <View style={[styles.donutSegment2, { borderColor: '#10B981' }]} />
              <View style={[styles.donutSegment3, { borderColor: '#6366F1' }]} />
              <View style={[styles.donutCenterHole, { backgroundColor: isDark ? '#101622' : '#FFFFFF' }]}>
                <Text style={[styles.donutCenterLabel, { color: colors.textMuted }]}>Tổng chi</Text>
                <Text style={[styles.donutCenterAmount, { color: colors.text }]}>
                  {monthExpense.toLocaleString('vi-VN')} ₫
                </Text>
              </View>
            </View>
          </View>

          {/* Legend Details */}
          <View style={styles.categoryList}>
            {categorySlices.map((item, idx) => (
              <View
                key={idx}
                style={[
                  styles.categoryCardItem,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View style={styles.catLeft}>
                  <View style={[styles.catDot, { backgroundColor: item.color }]} />
                  <Text style={[styles.catName, { color: colors.text }]}>
                    {item.name}
                  </Text>
                </View>
                <View style={styles.catRight}>
                  <Text style={[styles.catAmount, { color: colors.textMuted }]}>
                    {item.amount.toLocaleString('vi-VN')} ₫
                  </Text>
                  <Text style={[styles.catPercent, { color: colors.text }]}>
                    {item.percentage}%
                  </Text>
                </View>
              </View>
            ))}
          </View>
        </View>

        {/* ========================================================
            BLOCK 07: FINANCIAL ATTENTION ("Cần chú ý")
            ======================================================== */}
        <View style={styles.attentionSection}>
          <View style={styles.sectionHeaderRow}>
            <View style={styles.attentionHeadingRow}>
              <SymbolView
                name={{ ios: 'exclamationmark.triangle.fill', android: 'warning', web: 'warning' }}
                size={16}
                tintColor="#D97706"
              />
              <Text style={[styles.sectionTitleText, { color: colors.text, marginLeft: 6 }]}>
                Cần chú ý
              </Text>
              <View style={styles.attentionCountBadge}>
                <Text style={styles.attentionCountText}>2</Text>
              </View>
            </View>
          </View>

          <View style={styles.attentionCardsStack}>
            {attentionItems.map((att) => (
              <View
                key={att.id}
                style={[
                  styles.attentionCard,
                  {
                    backgroundColor: isDark ? '#23180D' : '#FEF3C7',
                    borderColor: isDark ? 'rgba(245, 158, 11, 0.3)' : '#FDE68A',
                  },
                ]}
              >
                <View style={styles.attentionCardBody}>
                  <Text style={[styles.attentionCardTitle, { color: isDark ? '#FDE68A' : '#92400E' }]}>
                    {att.title}
                  </Text>
                  <Text style={[styles.attentionCardDesc, { color: isDark ? '#D97706' : '#B45309' }]}>
                    {att.description}
                  </Text>
                </View>
                <Pressable
                  onPress={() => router.navigate(att.route as any)}
                  style={[styles.attentionActionBtn, { backgroundColor: isDark ? '#78350F' : '#D97706' }]}
                >
                  <Text style={styles.attentionActionText}>{att.actionLabel}</Text>
                </Pressable>
              </View>
            ))}
          </View>
        </View>

        {/* ========================================================
            BLOCK 08: GROUP FUNDS SUMMARY ("Quỹ nhóm của bạn")
            ======================================================== */}
        <View style={[styles.contentCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
          <View style={styles.sectionHeaderRow}>
            <View>
              <Text style={[styles.sectionTitleText, { color: colors.text }]}>
                Quỹ nhóm của bạn
              </Text>
              <Text style={[styles.sectionSubText, { color: colors.textMuted }]}>
                Quản lý thu chi và đóng góp nhóm
              </Text>
            </View>
            <Pressable
              onPress={() => router.navigate('/(authenticated)/friends')}
              style={styles.detailActionBtn}
            >
              <Text style={[styles.detailActionText, { color: colors.primary }]}>
                Xem tất cả quỹ nhóm →
              </Text>
            </Pressable>
          </View>

          <View style={styles.groupsStack}>
            {groupFunds.map((grp) => (
              <View
                key={grp.id}
                style={[
                  styles.groupCardItem,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View style={styles.groupTopRow}>
                  <View style={styles.groupMeta}>
                    <Text style={[styles.groupName, { color: colors.text }]}>
                      {grp.name}
                    </Text>
                    <Text style={[styles.groupSubtitle, { color: colors.textMuted }]}>
                      {grp.membersCount} thành viên • {grp.cycle}
                    </Text>
                  </View>
                  <View style={[styles.groupStatusPill, { backgroundColor: colors.primarySoft }]}>
                    <Text style={[styles.groupStatusText, { color: colors.primary }]}>
                      {grp.status}
                    </Text>
                  </View>
                </View>

                <View style={styles.groupBottomRow}>
                  <View>
                    <Text style={[styles.groupAmountLabel, { color: colors.textMuted }]}>
                      Số dư quỹ
                    </Text>
                    <Text style={[styles.groupAmountValue, { color: colors.text }]}>
                      {grp.balance.toLocaleString('vi-VN')} ₫
                    </Text>
                  </View>
                  {grp.obligation ? (
                    <View style={styles.groupObligationBox}>
                      <Text style={[styles.groupAmountLabel, { color: colors.textMuted }]}>
                        Nghĩa vụ cá nhân
                      </Text>
                      <Text style={[styles.groupAmountValue, { color: colors.danger }]}>
                        {grp.obligation.toLocaleString('vi-VN')} ₫
                      </Text>
                    </View>
                  ) : null}
                </View>
              </View>
            ))}
          </View>
        </View>

        {/* ========================================================
            BLOCK 09: SAVINGS GOALS SUMMARY ("Mục tiêu tiết kiệm")
            ======================================================== */}
        <View style={[styles.contentCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
          <View style={styles.sectionHeaderRow}>
            <View>
              <Text style={[styles.sectionTitleText, { color: colors.text }]}>
                Mục tiêu tiết kiệm
              </Text>
              <Text style={[styles.sectionSubText, { color: colors.textMuted }]}>
                Tiến độ tích lũy tài sản
              </Text>
            </View>
            <Pressable
              onPress={() => router.navigate('/(authenticated)/planning')}
              style={styles.detailActionBtn}
            >
              <Text style={[styles.detailActionText, { color: colors.primary }]}>
                Tạo mục tiêu mới +
              </Text>
            </Pressable>
          </View>

          <View style={styles.goalsStack}>
            {savingsGoals.map((g) => (
              <View
                key={g.id}
                style={[
                  styles.goalCardItem,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View style={styles.goalTitleRow}>
                  <Text style={[styles.goalName, { color: colors.text }]}>
                    {g.name}
                  </Text>
                  <Text style={[styles.goalPercent, { color: colors.primary }]}>
                    {g.progress}%
                  </Text>
                </View>

                {/* Progress bar */}
                <View style={[styles.progressBarTrack, { backgroundColor: isDark ? '#1E293B' : '#E2E8F0' }]}>
                  <View
                    style={[
                      styles.progressBarFill,
                      {
                        width: `${g.progress}%`,
                        backgroundColor: colors.primary,
                      },
                    ]}
                  />
                </View>

                <View style={styles.goalAmountsRow}>
                  <Text style={[styles.goalAmountText, { color: colors.textMuted }]}>
                    {g.current.toLocaleString('vi-VN')} ₫
                  </Text>
                  <Text style={[styles.goalAmountText, { color: colors.textMuted }]}>
                    Mục tiêu: {g.target.toLocaleString('vi-VN')} ₫
                  </Text>
                </View>
              </View>
            ))}
          </View>
        </View>

        {/* ========================================================
            BLOCK 10: EXACTLY 5 RECENT TRANSACTIONS
            ======================================================== */}
        <View style={[styles.contentCard, { backgroundColor: isDark ? '#101622' : '#FFFFFF', borderColor: colors.border }]}>
          <View style={styles.sectionHeaderRow}>
            <View>
              <Text style={[styles.sectionTitleText, { color: colors.text }]}>
                Giao dịch gần đây
              </Text>
              <Text style={[styles.sectionSubText, { color: colors.textMuted }]}>
                5 giao dịch phát sinh mới nhất
              </Text>
            </View>
            <Pressable
              onPress={() => router.navigate('/(authenticated)/wallet')}
              style={styles.detailActionBtn}
            >
              <Text style={[styles.detailActionText, { color: colors.primary }]}>
                Xem tất cả →
              </Text>
            </Pressable>
          </View>

          <View style={styles.transactionsList}>
            {recentTransactions.map((tx) => (
              <View
                key={tx.id}
                style={[
                  styles.transactionCardRow,
                  {
                    backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View
                  style={[
                    styles.txIconBox,
                    {
                      backgroundColor:
                        tx.type === 'income'
                          ? 'rgba(16, 185, 129, 0.12)'
                          : 'rgba(239, 68, 68, 0.12)',
                    },
                  ]}
                >
                  <SymbolView
                    name={{ ios: tx.iconName, android: tx.iconAndroid, web: tx.iconAndroid } as any}
                    size={16}
                    tintColor={tx.type === 'income' ? colors.success : colors.danger}
                  />
                </View>
                <View style={styles.txInfo}>
                  <Text style={[styles.txTitle, { color: colors.text }]}>
                    {tx.title}
                  </Text>
                  <Text style={[styles.txMeta, { color: colors.textMuted }]}>
                    {tx.category} • {tx.time}
                  </Text>
                </View>
                <Text
                  style={[
                    styles.txAmount,
                    { color: tx.type === 'income' ? colors.success : colors.text },
                  ]}
                >
                  {tx.amount > 0 ? '+' : ''}
                  {tx.amount.toLocaleString('vi-VN')} ₫
                </Text>
              </View>
            ))}
          </View>
        </View>

        <LogoutButton />
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    paddingBottom: 90, // Generous padding so content is not covered by Floating AI and Bottom Nav
  },
  // Block 1: Header + Search
  headerBlock: {
    gap: spacing.sm,
  },
  topBarRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.xs,
  },
  userProfileBlock: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm + 2,
  },
  avatarCircle: {
    width: 44,
    height: 44,
    borderRadius: 22,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  onlineDot: {
    position: 'absolute',
    bottom: 0,
    right: 0,
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: '#10B981',
    borderWidth: 1.5,
    borderColor: '#FFFFFF',
  },
  userTextBlock: {
    gap: 1,
  },
  welcomeGreeting: {
    fontSize: 12,
    fontWeight: '500',
  },
  welcomeName: {
    ...fontStyles.display,
    fontSize: 18,
    lineHeight: 24,
    fontWeight: '700',
  },
  topRightActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  headerIconBtn: {
    width: 40,
    height: 40,
    borderRadius: radius.md,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  notifBadgeDot: {
    position: 'absolute',
    top: 9,
    right: 9,
    width: 7,
    height: 7,
    borderRadius: 4,
    backgroundColor: '#EF4444',
  },
  searchSectionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  premiumSearchBox: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    height: 44,
    borderRadius: 18,
    borderWidth: 1,
    paddingHorizontal: spacing.md,
    gap: spacing.sm,
  },
  searchInputText: {
    flex: 1,
    fontSize: 13,
    fontWeight: '400',
  },
  filterPillBtn: {
    width: 44,
    height: 44,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },

  // Block 2: Obsidian Emerald Card
  obsidianCard: {
    borderRadius: 20,
    backgroundColor: '#064E3B',
    padding: spacing.lg,
    gap: spacing.md,
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.18,
    shadowRadius: 16,
    elevation: 8,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  walletBadgeWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  walletBadgeLabel: {
    color: '#A7F3D0',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.8,
  },
  eyeButton: {
    padding: 4,
  },
  balanceContainer: {
    paddingVertical: spacing.xs,
  },
  balanceNumber: {
    color: '#FFFFFF',
    fontSize: 32,
    fontWeight: '800',
    letterSpacing: -0.5,
  },
  cardBottomRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: spacing.xs,
  },
  accumulationPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: 'rgba(255, 255, 255, 0.12)',
    paddingVertical: 4,
    paddingHorizontal: 10,
    borderRadius: radius.pill,
  },
  accumulationDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: '#10B981',
  },
  accumulationText: {
    color: '#D1FAE5',
    fontSize: 11,
    fontWeight: '600',
  },
  cardSafetyStatus: {
    color: 'rgba(255, 255, 255, 0.7)',
    fontSize: 11,
    fontWeight: '500',
  },

  // Block 3: Quick Actions Row (4 buttons)
  quickActionsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.xs,
  },
  quickActionItem: {
    flex: 1,
    alignItems: 'center',
    gap: 6,
  },
  quickActionIconBox: {
    width: 52,
    height: 52,
    borderRadius: 16,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  quickActionLabel: {
    fontSize: 12,
    fontWeight: '600',
    textAlign: 'center',
  },

  // Block 4: 2x2 KPI Grid
  kpiGridContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  kpiCard: {
    width: '48.5%',
    borderRadius: 16,
    borderWidth: 1,
    padding: spacing.md - 2,
    gap: 6,
  },
  kpiHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  kpiLabel: {
    fontSize: 11,
    fontWeight: '500',
  },
  kpiIconDot: {
    width: 22,
    height: 22,
    borderRadius: 11,
    alignItems: 'center',
    justifyContent: 'center',
  },
  kpiValue: {
    fontSize: 15,
    fontWeight: '700',
    letterSpacing: -0.2,
  },

  // Shared content card
  contentCard: {
    borderRadius: 20,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.md,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sectionTitleText: {
    ...fontStyles.heading,
    fontSize: 15,
    fontWeight: '700',
  },
  sectionSubText: {
    fontSize: 11,
    marginTop: 1,
  },
  detailActionBtn: {
    paddingVertical: 2,
    paddingHorizontal: 4,
  },
  detailActionText: {
    fontSize: 12,
    fontWeight: '600',
  },

  // Block 5: Chart Controls & Bars
  chartModeToggle: {
    flexDirection: 'row',
    borderRadius: radius.md,
    padding: 2,
  },
  chartModeBtn: {
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: radius.sm,
  },
  chartModeText: {
    fontSize: 11,
  },
  chartContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    height: 95,
    paddingTop: spacing.xs,
  },
  chartCol: {
    alignItems: 'center',
    gap: 6,
    flex: 1,
  },
  barsGroup: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 3,
    height: 75,
  },
  barItem: {
    width: 9,
    borderRadius: 4,
  },
  chartMonthLabel: {
    fontSize: 11,
    fontWeight: '600',
  },
  chartSummaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    borderTopWidth: 1,
    paddingTop: spacing.sm,
  },
  chartSummaryItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
  },
  legendDot: {
    width: 7,
    height: 7,
    borderRadius: 4,
  },
  chartSummaryLabel: {
    fontSize: 11,
    fontWeight: '500',
  },
  chartSummaryAmount: {
    fontSize: 12,
    fontWeight: '700',
  },

  // Block 6: Donut Graphic & List
  flowToggle: {
    flexDirection: 'row',
    borderRadius: radius.md,
    padding: 2,
  },
  flowButton: {
    paddingVertical: 4,
    paddingHorizontal: 10,
    borderRadius: radius.sm,
  },
  flowText: {
    fontSize: 11,
  },
  donutGraphicBox: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: spacing.xs,
  },
  donutOuterRing: {
    width: 130,
    height: 130,
    borderRadius: 65,
    borderWidth: 12,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  donutSegment1: {
    position: 'absolute',
    width: 130,
    height: 130,
    borderRadius: 65,
    borderWidth: 12,
    borderRightColor: 'transparent',
    borderBottomColor: 'transparent',
  },
  donutSegment2: {
    position: 'absolute',
    width: 130,
    height: 130,
    borderRadius: 65,
    borderWidth: 12,
    borderTopColor: 'transparent',
    borderLeftColor: 'transparent',
  },
  donutSegment3: {
    position: 'absolute',
    width: 130,
    height: 130,
    borderRadius: 65,
    borderWidth: 12,
    borderLeftColor: 'transparent',
    borderBottomColor: 'transparent',
  },
  donutCenterHole: {
    width: 86,
    height: 86,
    borderRadius: 43,
    alignItems: 'center',
    justifyContent: 'center',
  },
  donutCenterLabel: {
    fontSize: 10,
    fontWeight: '500',
  },
  donutCenterAmount: {
    fontSize: 12,
    fontWeight: '700',
    marginTop: 1,
  },
  categoryList: {
    gap: spacing.xs + 2,
  },
  categoryCardItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  catLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  catDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  catName: {
    fontSize: 12,
    fontWeight: '600',
  },
  catRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  catAmount: {
    fontSize: 11,
  },
  catPercent: {
    fontSize: 12,
    fontWeight: '700',
    minWidth: 32,
    textAlign: 'right',
  },

  // Block 7: Financial Attention
  attentionSection: {
    gap: spacing.sm,
  },
  attentionHeadingRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  attentionCountBadge: {
    marginLeft: 6,
    backgroundColor: '#D97706',
    borderRadius: 10,
    width: 18,
    height: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  attentionCountText: {
    color: '#FFFFFF',
    fontSize: 10,
    fontWeight: '700',
  },
  attentionCardsStack: {
    gap: spacing.sm,
  },
  attentionCard: {
    borderRadius: 16,
    borderWidth: 1,
    padding: spacing.md,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm,
  },
  attentionCardBody: {
    flex: 1,
    gap: 2,
  },
  attentionCardTitle: {
    fontSize: 12,
    fontWeight: '700',
  },
  attentionCardDesc: {
    fontSize: 11,
  },
  attentionActionBtn: {
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: radius.md,
  },
  attentionActionText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '700',
  },

  // Block 8: Group Funds
  groupsStack: {
    gap: spacing.sm,
  },
  groupCardItem: {
    borderRadius: 16,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.sm,
  },
  groupTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  groupMeta: {
    gap: 1,
  },
  groupName: {
    fontSize: 13,
    fontWeight: '700',
  },
  groupSubtitle: {
    fontSize: 11,
  },
  groupStatusPill: {
    paddingVertical: 3,
    paddingHorizontal: 8,
    borderRadius: radius.pill,
  },
  groupStatusText: {
    fontSize: 10,
    fontWeight: '700',
  },
  groupBottomRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 4,
  },
  groupAmountLabel: {
    fontSize: 10,
  },
  groupAmountValue: {
    fontSize: 13,
    fontWeight: '700',
  },
  groupObligationBox: {
    alignItems: 'flex-end',
  },

  // Block 9: Savings Goals
  goalsStack: {
    gap: spacing.sm,
  },
  goalCardItem: {
    borderRadius: 16,
    borderWidth: 1,
    padding: spacing.md,
    gap: 6,
  },
  goalTitleRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  goalName: {
    fontSize: 13,
    fontWeight: '700',
  },
  goalPercent: {
    fontSize: 13,
    fontWeight: '800',
  },
  progressBarTrack: {
    height: 8,
    borderRadius: 4,
    overflow: 'hidden',
  },
  progressBarFill: {
    height: '100%',
    borderRadius: 4,
  },
  goalAmountsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  goalAmountText: {
    fontSize: 11,
  },

  // Block 10: 5 Recent Transactions
  transactionsList: {
    gap: spacing.xs + 2,
  },
  transactionCardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    gap: spacing.sm,
  },
  txIconBox: {
    width: 38,
    height: 38,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  txInfo: {
    flex: 1,
    gap: 2,
  },
  txTitle: {
    fontSize: 13,
    fontWeight: '600',
  },
  txMeta: {
    fontSize: 11,
  },
  txAmount: {
    fontSize: 13,
    fontWeight: '700',
  },
});
