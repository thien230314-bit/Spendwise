import React, { useState } from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SymbolView } from 'expo-symbols';
import { AppScreen } from '@/components/AppScreen';
import { Badge, Card, Copy, UIIcon } from '@/components/MobileUI';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function PlanningScreen() {
  const { colors, isDark } = useTheme();
  const [activeTab, setActiveTab] = useState<'budget' | 'goal' | 'recurring'>('budget');

  const budgets = [
    { name: 'Ăn uống & Cà phê', spent: 4200000, limit: 5000000, color: '#10B981', pct: 84 },
    { name: 'Mua sắm & Tiêu dùng', spent: 2800000, limit: 4000000, color: '#6366F1', pct: 70 },
    { name: 'Đi lại & Xăng xe', spent: 1800000, limit: 2500000, color: '#F59E0B', pct: 72 },
    { name: 'Hóa đơn & Dịch vụ', spent: 1200000, limit: 1500000, color: '#EC4899', pct: 80 },
  ];

  const goals = [
    { name: 'Quỹ khẩn cấp 6 tháng', current: 36000000, target: 50000000, date: '12/2026', color: '#10B981', pct: 72 },
    { name: 'Du lịch Nhật Bản mùa xuân', current: 15000000, target: 30000000, date: '04/2027', color: '#06B6D4', pct: 50 },
    { name: 'Đổi máy tính làm việc', current: 22000000, target: 25000000, date: '11/2026', color: '#8B5CF6', pct: 88 },
  ];

  const recurrings = [
    { name: 'Tiền thuê căn hộ', amount: 8500000, due: 'Ngày 05 hàng tháng', type: 'expense' as const, status: 'Đã thanh toán' },
    { name: 'Gói mạng Internet FPT', amount: 285000, due: 'Ngày 15 hàng tháng', type: 'expense' as const, status: 'Sắp tới hạn' },
    { name: 'Đăng ký Spotify Family', amount: 99000, due: 'Ngày 22 hàng tháng', type: 'expense' as const, status: 'Tự động trừ' },
  ];

  return (
    <AppScreen
      title="Kế hoạch tài chính"
      subtitle="Theo dõi hạn mức ngân sách, mục tiêu tích lũy và thu chi định kỳ."
    >
      <View style={styles.container}>
        {/* 1. Horizontal Tab Filter Pills (Reference Images 1, 2, 3) */}
        <View
          style={[
            styles.segmentRow,
            {
              backgroundColor: isDark ? '#101622' : '#F1F5F9',
              borderColor: colors.border,
            },
          ]}
        >
          {[
            { id: 'budget', label: 'Ngân sách' },
            { id: 'goal', label: 'Mục tiêu' },
            { id: 'recurring', label: 'Định kỳ' },
          ].map((tab) => {
            const isSelected = activeTab === tab.id;
            return (
              <Pressable
                key={tab.id}
                onPress={() => setActiveTab(tab.id as any)}
                style={[
                  styles.tabPillBtn,
                  isSelected && {
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
                    styles.tabPillText,
                    {
                      color: isSelected ? (isDark ? '#34D399' : '#047857') : colors.textMuted,
                      fontWeight: isSelected ? '700' : '500',
                    },
                  ]}
                >
                  {tab.label}
                </Text>
              </Pressable>
            );
          })}
        </View>

        {/* 2. Content Sections based on Selected Tab */}
        {activeTab === 'budget' && (
          <View style={styles.tabContentBlock}>
            {/* Overall Monthly Budget Hero Card */}
            <View
              style={[
                styles.summaryCard,
                {
                  backgroundColor: isDark ? '#101622' : '#FFFFFF',
                  borderColor: colors.border,
                },
              ]}
            >
              <View style={styles.cardHeaderRow}>
                <View>
                  <Text style={[styles.cardHeaderSubhead, { color: colors.textMuted }]}>
                    Tổng ngân sách tháng 10/2026
                  </Text>
                  <Text style={[styles.cardHeaderAmount, { color: colors.text }]}>
                    15.000.000 ₫
                  </Text>
                </View>
                <View style={[styles.pctBadge, { backgroundColor: colors.primarySoft }]}>
                  <Text style={[styles.pctBadgeText, { color: colors.primary }]}>
                    Đã chi 62%
                  </Text>
                </View>
              </View>

              <View style={[styles.progressTrack, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
                <View style={[styles.progressFill, { width: '62%', backgroundColor: colors.primary }]} />
              </View>

              <View style={styles.budgetMetaRow}>
                <Text style={[styles.metaText, { color: colors.textMuted }]}>
                  Đã tiêu: 9.300.000 ₫
                </Text>
                <Text style={[styles.metaText, { color: colors.textMuted }]}>
                  Còn lại: 5.700.000 ₫ (An toàn)
                </Text>
              </View>
            </View>

            {/* Category Budgets List (Modern Rounded Card Items) */}
            <View style={styles.listSection}>
              <View style={styles.sectionTitleRow}>
                <Text style={[styles.sectionHeading, { color: colors.text }]}>
                  Hạn mức từng danh mục
                </Text>
                <Text style={[styles.createLink, { color: colors.primary }]}>
                  + Thêm hạn mức
                </Text>
              </View>

              {budgets.map((b, idx) => (
                <View
                  key={idx}
                  style={[
                    styles.itemCard,
                    {
                      backgroundColor: isDark ? '#101622' : '#FFFFFF',
                      borderColor: colors.border,
                    },
                  ]}
                >
                  <View style={styles.itemHeader}>
                    <View style={styles.itemNameWrap}>
                      <View style={[styles.dotCircle, { backgroundColor: b.color }]} />
                      <Text style={[styles.itemName, { color: colors.text }]}>
                        {b.name}
                      </Text>
                    </View>
                    <Text style={[styles.itemPct, { color: b.pct > 80 ? colors.danger : colors.text }]}>
                      {b.pct}%
                    </Text>
                  </View>

                  <View style={[styles.progressTrack, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
                    <View
                      style={[
                        styles.progressFill,
                        {
                          width: `${b.pct}%`,
                          backgroundColor: b.pct > 80 ? colors.danger : b.color,
                        },
                      ]}
                    />
                  </View>

                  <View style={styles.itemFooter}>
                    <Text style={[styles.itemSpent, { color: colors.textMuted }]}>
                      Đã chi: {b.spent.toLocaleString('vi-VN')} ₫
                    </Text>
                    <Text style={[styles.itemLimit, { color: colors.textMuted }]}>
                      Hạn mức: {b.limit.toLocaleString('vi-VN')} ₫
                    </Text>
                  </View>
                </View>
              ))}
            </View>
          </View>
        )}

        {activeTab === 'goal' && (
          <View style={styles.tabContentBlock}>
            <View style={styles.sectionTitleRow}>
              <Text style={[styles.sectionHeading, { color: colors.text }]}>
                Mục tiêu tiết kiệm đang thực hiện
              </Text>
              <Text style={[styles.createLink, { color: colors.primary }]}>
                + Tạo mục tiêu
              </Text>
            </View>

            {goals.map((g, idx) => (
              <View
                key={idx}
                style={[
                  styles.itemCard,
                  {
                    backgroundColor: isDark ? '#101622' : '#FFFFFF',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View style={styles.itemHeader}>
                  <View style={styles.itemNameWrap}>
                    <View style={[styles.dotCircle, { backgroundColor: g.color }]} />
                    <Text style={[styles.itemName, { color: colors.text }]}>
                      {g.name}
                    </Text>
                  </View>
                  <Text style={[styles.itemPct, { color: colors.primary }]}>
                    {g.pct}%
                  </Text>
                </View>

                <View style={[styles.progressTrack, { backgroundColor: isDark ? '#1E293B' : '#F1F5F9' }]}>
                  <View style={[styles.progressFill, { width: `${g.pct}%`, backgroundColor: g.color }]} />
                </View>

                <View style={styles.itemFooter}>
                  <Text style={[styles.itemSpent, { color: colors.text }]}>
                    Tích lũy: {g.current.toLocaleString('vi-VN')} ₫
                  </Text>
                  <Text style={[styles.itemLimit, { color: colors.textMuted }]}>
                    Mục tiêu: {g.target.toLocaleString('vi-VN')} ₫ ({g.date})
                  </Text>
                </View>
              </View>
            ))}
          </View>
        )}

        {activeTab === 'recurring' && (
          <View style={styles.tabContentBlock}>
            <View style={styles.sectionTitleRow}>
              <Text style={[styles.sectionHeading, { color: colors.text }]}>
                Hóa đơn & Giao dịch định kỳ
              </Text>
              <Text style={[styles.createLink, { color: colors.primary }]}>
                + Thêm lịch
              </Text>
            </View>

            {recurrings.map((r, idx) => (
              <View
                key={idx}
                style={[
                  styles.itemCard,
                  {
                    backgroundColor: isDark ? '#101622' : '#FFFFFF',
                    borderColor: colors.border,
                  },
                ]}
              >
                <View style={styles.itemHeader}>
                  <View style={styles.itemNameWrap}>
                    <SymbolView
                      name={{ ios: 'calendar.badge.clock', android: 'schedule', web: 'schedule' }}
                      size={18}
                      tintColor={colors.primary}
                    />
                    <Text style={[styles.itemName, { color: colors.text }]}>
                      {r.name}
                    </Text>
                  </View>
                  <View style={[styles.pctBadge, { backgroundColor: colors.primarySoft }]}>
                    <Text style={[styles.pctBadgeText, { color: colors.primary }]}>
                      {r.status}
                    </Text>
                  </View>
                </View>

                <View style={styles.itemFooter}>
                  <Text style={[styles.itemSpent, { color: colors.danger, fontWeight: '700' }]}>
                    −{r.amount.toLocaleString('vi-VN')} ₫
                  </Text>
                  <Text style={[styles.itemLimit, { color: colors.textMuted }]}>
                    {r.due}
                  </Text>
                </View>
              </View>
            ))}
          </View>
        )}
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    paddingBottom: spacing.xxl,
  },
  segmentRow: {
    flexDirection: 'row',
    padding: 3,
    borderRadius: 14,
    borderWidth: 1,
  },
  tabPillBtn: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 11,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tabPillText: {
    fontSize: 12,
  },
  tabContentBlock: {
    gap: spacing.md,
  },
  summaryCard: {
    borderRadius: 22,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.sm + 2,
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
  cardHeaderSubhead: {
    fontSize: 11,
    fontWeight: '500',
  },
  cardHeaderAmount: {
    fontSize: 24,
    fontWeight: '800',
    fontFamily: 'monospace',
    letterSpacing: -0.4,
    marginTop: 2,
  },
  pctBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 12,
  },
  pctBadgeText: {
    fontSize: 10,
    fontWeight: '700',
  },
  progressTrack: {
    height: 8,
    borderRadius: 4,
    overflow: 'hidden',
  },
  progressFill: {
    height: '100%',
    borderRadius: 4,
  },
  budgetMetaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  metaText: {
    fontSize: 11,
    fontFamily: 'monospace',
  },
  listSection: {
    gap: spacing.sm,
  },
  sectionTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 2,
  },
  sectionHeading: {
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: -0.2,
  },
  createLink: {
    fontSize: 12,
    fontWeight: '600',
  },
  itemCard: {
    borderRadius: 18,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.sm,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.03,
    shadowRadius: 3,
    elevation: 1,
  },
  itemHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  itemNameWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  dotCircle: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  itemName: {
    fontSize: 13,
    fontWeight: '600',
  },
  itemPct: {
    fontSize: 12,
    fontWeight: '700',
    fontFamily: 'monospace',
  },
  itemFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  itemSpent: {
    fontSize: 11,
    fontFamily: 'monospace',
  },
  itemLimit: {
    fontSize: 11,
    fontFamily: 'monospace',
  },
});
