import React, { useState } from 'react';
import {
  Pressable,
  StyleSheet,
  Text,
  View,
  TextInput,
  Modal,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function PlanningScreen() {
  const { colors, isDark } = useTheme();
  const [activeTab, setActiveTab] = useState<'budget' | 'goal' | 'recurring'>('budget');
  const [isAddBudgetModalOpen, setIsAddBudgetModalOpen] = useState(false);
  const [newCategoryName, setNewCategoryName] = useState('');
  const [newCategoryLimit, setNewCategoryLimit] = useState('');

  const [budgets, setBudgets] = useState([
    { id: 'b-1', name: 'Ăn uống & Cà phê', spent: 4200000, limit: 5000000, color: colors.primary, pct: 84 },
    { id: 'b-2', name: 'Mua sắm & Tiêu dùng', spent: 2800000, limit: 4000000, color: colors.accent, pct: 70 },
    { id: 'b-3', name: 'Đi lại & Xăng xe', spent: 1800000, limit: 2500000, color: colors.amber, pct: 72 },
    { id: 'b-4', name: 'Hóa đơn & Dịch vụ', spent: 1200000, limit: 1500000, color: colors.violet, pct: 80 },
  ]);

  const goals = [
    { id: 'g-1', name: 'Quỹ khẩn cấp 6 tháng', current: 36000000, target: 50000000, date: '12/2026', pct: 72 },
    { id: 'g-2', name: 'Du lịch Nhật Bản 2027', current: 15000000, target: 30000000, date: '04/2027', pct: 50 },
    { id: 'g-3', name: 'Đổi máy tính làm việc', current: 22000000, target: 25000000, date: '11/2026', pct: 88 },
  ];

  const recurrings = [
    { id: 'r-1', name: 'Tiền thuê căn hộ', amount: 8500000, due: 'Ngày 05 hàng tháng', status: 'Đã thanh toán' },
    { id: 'r-2', name: 'Gói mạng Internet FPT', amount: 285000, due: 'Ngày 15 hàng tháng', status: 'Sắp tới hạn' },
    { id: 'r-3', name: 'Đăng ký Spotify Family', amount: 99000, due: 'Ngày 22 hàng tháng', status: 'Tự động trừ' },
  ];

  const totalSpent = budgets.reduce((acc, cur) => acc + cur.spent, 0);
  const totalLimit = budgets.reduce((acc, cur) => acc + cur.limit, 0);
  const overallPct = Math.round((totalSpent / totalLimit) * 100);

  const handleAddBudget = () => {
    const limit = parseInt(newCategoryLimit.replace(/\D/g, ''), 10);
    if (!newCategoryName.trim() || !limit || limit <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập tên danh mục và hạn mức');
      return;
    }
    const newB = {
      id: `b-${Date.now()}`,
      name: newCategoryName.trim(),
      spent: 0,
      limit,
      color: colors.primary,
      pct: 0,
    };
    setBudgets([...budgets, newB]);
    setIsAddBudgetModalOpen(false);
    setNewCategoryName('');
    setNewCategoryLimit('');
    Alert.alert('Thành công', `Đã thêm ngân sách "${newB.name}".`);
  };

  return (
    <AppScreen title="Kế hoạch tài chính" scrollable>
      <View style={styles.container}>
        {/* BACK NAV ROW */}
        <Pressable onPress={() => router.replace('/(authenticated)/home')} style={styles.backRow}>
          <Ionicons name="arrow-back" size={20} color={colors.primary} />
          <Text style={[styles.backText, { color: colors.primary }]}>Quay lại Trang chủ</Text>
        </Pressable>

        {/* 1. FILTER TABS */}
        <View style={[styles.segmentContainer, { backgroundColor: isDark ? colors.surfaceElevated : '#DCEEE4' }]}>
          <Pressable
            onPress={() => setActiveTab('budget')}
            style={[styles.segmentBtn, activeTab === 'budget' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: activeTab === 'budget' ? colors.primary : colors.textMuted }]}>
              Ngân sách
            </Text>
          </Pressable>

          <Pressable
            onPress={() => setActiveTab('goal')}
            style={[styles.segmentBtn, activeTab === 'goal' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: activeTab === 'goal' ? colors.primary : colors.textMuted }]}>
              Mục tiêu ({goals.length})
            </Text>
          </Pressable>

          <Pressable
            onPress={() => setActiveTab('recurring')}
            style={[styles.segmentBtn, activeTab === 'recurring' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: activeTab === 'recurring' ? colors.primary : colors.textMuted }]}>
              Định kỳ ({recurrings.length})
            </Text>
          </Pressable>
        </View>

        {/* 2. TAB CONTENT */}
        {activeTab === 'budget' && (
          <View style={styles.tabContentWrap}>
            {/* Overall Monthly Hero Progress */}
            <GlassCard style={styles.overallCard}>
              <View style={styles.overallTopRow}>
                <View>
                  <Text style={[styles.overallLabel, { color: colors.textMuted }]}>
                    Tổng chi tiêu ngân sách tháng 11
                  </Text>
                  <Text style={[styles.overallAmount, { color: colors.text }]}>
                    {totalSpent.toLocaleString('vi-VN')} ₫
                  </Text>
                </View>
                <View style={[styles.pctBadge, { backgroundColor: overallPct > 80 ? 'rgba(229,72,77,0.15)' : colors.primarySoft }]}>
                  <Text style={[styles.pctBadgeText, { color: overallPct > 80 ? colors.danger : colors.primary }]}>
                    {overallPct}%
                  </Text>
                </View>
              </View>

              <View style={[styles.overallTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
                <View
                  style={[
                    styles.overallFill,
                    {
                      width: `${Math.min(100, overallPct)}%`,
                      backgroundColor: overallPct > 80 ? colors.danger : colors.primary,
                    },
                  ]}
                />
              </View>

              <View style={styles.overallFooter}>
                <Text style={[styles.overallFooterText, { color: colors.textMuted }]}>
                  Hạn mức: {totalLimit.toLocaleString('vi-VN')} ₫ • Còn lại: {(totalLimit - totalSpent).toLocaleString('vi-VN')} ₫
                </Text>
              </View>
            </GlassCard>

            {/* Category Budgets Header + Add Button */}
            <View style={styles.sectionHeaderRow}>
              <Text style={[styles.sectionTitle, { color: colors.text }]}>Danh mục chi tiêu</Text>
              <Pressable onPress={() => setIsAddBudgetModalOpen(true)}>
                <Text style={[styles.addLink, { color: colors.primary }]}>+ Thêm danh mục</Text>
              </Pressable>
            </View>

            {/* Category Cards */}
            <View style={styles.budgetList}>
              {budgets.map((b) => (
                <GlassCard key={b.id} style={styles.categoryCard}>
                  <View style={styles.categoryHeader}>
                    <Text style={[styles.categoryName, { color: colors.text }]}>{b.name}</Text>
                    <Text style={[styles.categoryAmount, { color: colors.text }]}>
                      {b.spent.toLocaleString('vi-VN')} ₫ / {b.limit.toLocaleString('vi-VN')} ₫
                    </Text>
                  </View>

                  <View style={[styles.categoryTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
                    <View
                      style={[
                        styles.categoryFill,
                        {
                          width: `${Math.min(100, b.pct)}%`,
                          backgroundColor: b.pct > 80 ? colors.danger : b.color,
                        },
                      ]}
                    />
                  </View>

                  <View style={styles.categoryFooter}>
                    <Text
                      style={[
                        styles.categoryStatus,
                        { color: b.pct > 80 ? colors.danger : colors.textMuted },
                      ]}
                    >
                      {b.pct > 80 ? `Cảnh báo: Đã dùng ${b.pct}% hạn mức` : `Đã dùng ${b.pct}%`}
                    </Text>
                    <Text style={[styles.categoryRemaining, { color: colors.textMuted }]}>
                      Còn {(b.limit - b.spent).toLocaleString('vi-VN')} ₫
                    </Text>
                  </View>
                </GlassCard>
              ))}
            </View>
          </View>
        )}

        {activeTab === 'goal' && (
          <View style={styles.tabContentWrap}>
            <View style={styles.goalList}>
              {goals.map((g) => (
                <GlassCard key={g.id} style={styles.categoryCard}>
                  <View style={styles.categoryHeader}>
                    <Text style={[styles.categoryName, { color: colors.text }]}>{g.name}</Text>
                    <Text style={[styles.categoryAmount, { color: colors.primary }]}>
                      {g.pct}%
                    </Text>
                  </View>

                  <View style={[styles.categoryTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
                    <View style={[styles.categoryFill, { width: `${g.pct}%`, backgroundColor: colors.primary }]} />
                  </View>

                  <View style={styles.categoryFooter}>
                    <Text style={[styles.categoryRemaining, { color: colors.textMuted }]}>
                      Hiện có: {g.current.toLocaleString('vi-VN')} ₫ / Mục tiêu: {g.target.toLocaleString('vi-VN')} ₫
                    </Text>
                    <Text style={[styles.categoryRemaining, { color: colors.textMuted }]}>
                      Hạn: {g.date}
                    </Text>
                  </View>
                </GlassCard>
              ))}
            </View>
          </View>
        )}

        {activeTab === 'recurring' && (
          <View style={styles.tabContentWrap}>
            <View style={styles.recurringList}>
              {recurrings.map((r) => (
                <GlassCard key={r.id} style={styles.recurringCard}>
                  <View style={styles.recurringLeft}>
                    <View style={[styles.recurringIconBg, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                      <Ionicons name="repeat" size={20} color={colors.primary} />
                    </View>
                    <View>
                      <Text style={[styles.recurringName, { color: colors.text }]}>{r.name}</Text>
                      <Text style={[styles.recurringDue, { color: colors.textMuted }]}>{r.due}</Text>
                    </View>
                  </View>
                  <View style={styles.recurringRight}>
                    <Text style={[styles.recurringAmount, { color: colors.text }]}>
                      {r.amount.toLocaleString('vi-VN')} ₫
                    </Text>
                    <Text style={[styles.recurringStatus, { color: colors.primary }]}>{r.status}</Text>
                  </View>
                </GlassCard>
              ))}
            </View>
          </View>
        )}

        {/* ADD BUDGET MODAL */}
        <Modal
          visible={isAddBudgetModalOpen}
          transparent
          animationType="slide"
          onRequestClose={() => setIsAddBudgetModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Thêm danh mục ngân sách</Text>
                <Pressable onPress={() => setIsAddBudgetModalOpen(false)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.inputLabel, { color: colors.text }]}>Tên danh mục</Text>
                <TextInput
                  value={newCategoryName}
                  onChangeText={setNewCategoryName}
                  placeholder="Ví dụ: Giải trí & Xem phim"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 12 }]}>Hạn mức tháng (₫)</Text>
                <TextInput
                  value={newCategoryLimit}
                  onChangeText={setNewCategoryLimit}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 3000000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleAddBudget}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Lưu ngân sách</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>
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
  backRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 6,
  },
  backText: {
    fontSize: 14,
    fontWeight: '700',
  },
  segmentContainer: {
    flexDirection: 'row',
    borderRadius: radius.pill,
    padding: 4,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.pill,
  },
  segmentBtnActive: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  segmentText: {
    fontSize: 13,
    fontWeight: '700',
  },
  tabContentWrap: {
    gap: spacing.md,
  },
  overallCard: {
    padding: spacing.md,
    gap: 10,
  },
  overallTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  overallLabel: {
    fontSize: 12,
  },
  overallAmount: {
    fontSize: 24,
    fontWeight: '800',
    marginTop: 2,
  },
  pctBadge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.pill,
  },
  pctBadgeText: {
    fontSize: 13,
    fontWeight: '800',
  },
  overallTrack: {
    height: 8,
    borderRadius: 4,
    overflow: 'hidden',
  },
  overallFill: {
    height: '100%',
    borderRadius: 4,
  },
  overallFooter: {
    marginTop: 2,
  },
  overallFooterText: {
    fontSize: 12,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 4,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
  },
  addLink: {
    fontSize: 13,
    fontWeight: '700',
  },
  budgetList: {
    gap: 10,
  },
  categoryCard: {
    padding: spacing.md,
    gap: 8,
  },
  categoryHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  categoryName: {
    fontSize: 14,
    fontWeight: '700',
  },
  categoryAmount: {
    fontSize: 13,
    fontWeight: '600',
  },
  categoryTrack: {
    height: 6,
    borderRadius: 3,
    overflow: 'hidden',
  },
  categoryFill: {
    height: '100%',
    borderRadius: 3,
  },
  categoryFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 2,
  },
  categoryStatus: {
    fontSize: 11,
    fontWeight: '600',
  },
  categoryRemaining: {
    fontSize: 11,
  },
  goalList: {
    gap: 10,
  },
  recurringList: {
    gap: 10,
  },
  recurringCard: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
  },
  recurringLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  recurringIconBg: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  recurringName: {
    fontSize: 14,
    fontWeight: '700',
  },
  recurringDue: {
    fontSize: 12,
    marginTop: 2,
  },
  recurringRight: {
    alignItems: 'flex-end',
  },
  recurringAmount: {
    fontSize: 14,
    fontWeight: '800',
  },
  recurringStatus: {
    fontSize: 11,
    fontWeight: '600',
    marginTop: 2,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'flex-end',
  },
  modalSheet: {
    borderTopLeftRadius: radius.panel,
    borderTopRightRadius: radius.panel,
    padding: spacing.lg,
    paddingBottom: 36,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
  },
  modalBody: {
    gap: 8,
  },
  inputLabel: {
    fontSize: 13,
    fontWeight: '600',
  },
  modalInput: {
    height: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 14,
    fontSize: 15,
  },
  modalSubmitBtn: {
    marginTop: 16,
    paddingVertical: 14,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  modalSubmitText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
});
