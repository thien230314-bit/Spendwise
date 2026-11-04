import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  Pressable,
  TextInput,
  Modal,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { BalanceCard } from '@/components/BalanceCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';

export default function HomeScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();

  // State
  const [isBalanceHidden, setIsBalanceHidden] = useState(false);
  const [availableBalance, setAvailableBalance] = useState(14250000);
  const [quickModal, setQuickModal] = useState<'deposit' | 'withdraw' | 'manual_tx' | null>(null);

  // Form states for modals
  const [modalAmount, setModalAmount] = useState('');
  const [modalNote, setModalNote] = useState('');
  const [modalCategory, setModalCategory] = useState('Ăn uống');

  const spendId = (user as any)?.spendId || (user as any)?.accountNumber || 'SW-USER-8888';
  const fullName = user?.fullName || 'Người dùng SpendWise';

  // Greeting
  const getGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return 'Chào buổi sáng';
    if (hour < 18) return 'Chào buổi chiều';
    return 'Chào buổi tối';
  };

  // Recent transactions
  const recentTransactions = [
    {
      id: 'tx-1',
      title: 'Siêu thị WinMart Times City',
      category: 'Ăn uống & Nhu yếu phẩm',
      amount: -845000,
      time: '18:45 Hôm nay',
      type: 'expense',
    },
    {
      id: 'tx-2',
      title: 'Đóng góp Quỹ Du lịch Đà Nẵng',
      category: 'Quỹ nhóm Cố định V2',
      amount: -1500000,
      time: '14:15 Hôm qua',
      type: 'fund',
    },
    {
      id: 'tx-3',
      title: 'Lương TechCorp tháng 10',
      category: 'Thu nhập chính',
      amount: 35000000,
      time: '01/11/2026',
      type: 'income',
    },
    {
      id: 'tx-4',
      title: 'Cà phê Highland Coffee',
      category: 'Ăn uống',
      amount: -65000,
      time: '31/10/2026',
      type: 'expense',
    },
  ];

  const handleDepositSubmit = () => {
    const amt = parseInt(modalAmount.replace(/\D/g, ''), 10);
    if (!amt || amt <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ');
      return;
    }
    setAvailableBalance((prev) => prev + amt);
    setQuickModal(null);
    setModalAmount('');
    Alert.alert('Thành công', `Đã nạp ${amt.toLocaleString('vi-VN')} ₫ vào Ví chi tiêu chính.`);
  };

  const handleWithdrawSubmit = () => {
    const amt = parseInt(modalAmount.replace(/\D/g, ''), 10);
    if (!amt || amt <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ');
      return;
    }
    if (amt > availableBalance) {
      Alert.alert('Lỗi', 'Số dư chi tiêu khả dụng không đủ');
      return;
    }
    setAvailableBalance((prev) => prev - amt);
    setQuickModal(null);
    setModalAmount('');
    Alert.alert('Thành công', `Đã tạo lệnh rút ${amt.toLocaleString('vi-VN')} ₫ về tài khoản ngân hàng liên kết.`);
  };

  const handleManualTxSubmit = () => {
    const amt = parseInt(modalAmount.replace(/\D/g, ''), 10);
    if (!amt || amt <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền');
      return;
    }
    setAvailableBalance((prev) => Math.max(0, prev - amt));
    setQuickModal(null);
    setModalAmount('');
    setModalNote('');
    Alert.alert('Đã ghi nhận', `Đã ghi nhận khoản chi ${amt.toLocaleString('vi-VN')} ₫ cho "${modalCategory}".`);
  };

  return (
    <AppScreen title="" scrollable>
      <View style={styles.container}>
        {/* 1. HEADER: GREETING & QUICK ACCESS */}
        <View style={styles.headerRow}>
          <View style={styles.greetingCol}>
            <Text style={[styles.greetingSub, { color: colors.textMuted }]}>{getGreeting()},</Text>
            <Text style={[styles.greetingName, { color: colors.text }]}>{fullName}</Text>
          </View>

          <View style={styles.headerActions}>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Trợ lý tài chính AI"
              onPress={() => router.push('/(authenticated)/ai-chat')}
              style={[styles.headerIconBtn, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}
            >
              <Ionicons name="sparkles" size={20} color={colors.primary} />
            </Pressable>

            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Mã QR & Quét"
              onPress={() => router.push('/(authenticated)/qr')}
              style={[styles.headerIconBtn, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}
            >
              <Ionicons name="qr-code-outline" size={20} color={colors.primary} />
            </Pressable>
          </View>
        </View>

        {/* 2. HERO BALANCE CARD (WITH "Tất cả ví" LINK) */}
        <BalanceCard
          availableBalance={availableBalance}
          isBalanceHidden={isBalanceHidden}
          onToggleHideBalance={() => setIsBalanceHidden((prev) => !prev)}
          spendId={spendId}
          onOpenWallets={() => router.push('/(authenticated)/wallet')}
          onTransfer={() => router.push('/(authenticated)/transfer')}
          onDeposit={() => setQuickModal('deposit')}
          onWithdraw={() => setQuickModal('withdraw')}
          onAddManualTx={() => setQuickModal('manual_tx')}
        />

        {/* 3. CẦN XỬ LÝ NGAY (NEEDS ATTENTION ACTION QUEUE) */}
        <View style={styles.sectionWrap}>
          <View style={styles.sectionHeaderRow}>
            <View style={styles.attentionTag}>
              <View style={[styles.attentionDot, { backgroundColor: colors.amber }]} />
              <Text style={[styles.sectionTitle, { color: colors.text }]}>Cần xử lý ngay</Text>
            </View>
            <View style={[styles.countPill, { backgroundColor: 'rgba(217, 130, 24, 0.15)' }]}>
              <Text style={[styles.countPillText, { color: colors.amber }]}>2 việc</Text>
            </View>
          </View>

          <GlassCard style={styles.attentionCard}>
            {/* Attention Item 1 */}
            <Pressable
              onPress={() => router.push('/(authenticated)/groups')}
              style={[styles.attentionRow, { borderBottomColor: colors.border }]}
            >
              <View style={[styles.attentionIconWrap, { backgroundColor: isDark ? '#382A14' : '#FEF3C7' }]}>
                <Ionicons name="time-outline" size={20} color={colors.amber} />
              </View>
              <View style={styles.attentionInfo}>
                <Text style={[styles.attentionTitle, { color: colors.text }]}>
                  Quỹ Du lịch Đà Nẵng: Yêu cầu chi 3.000.000 ₫
                </Text>
                <Text style={[styles.attentionMeta, { color: colors.textMuted }]}>
                  Thời hạn phản đối 24h còn: 14 giờ 20 phút
                </Text>
              </View>
              <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
            </Pressable>

            {/* Attention Item 2 */}
            <Pressable
              onPress={() => router.push('/(authenticated)/friends')}
              style={styles.attentionRow}
            >
              <View style={[styles.attentionIconWrap, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons name="person-add-outline" size={20} color={colors.primary} />
              </View>
              <View style={styles.attentionInfo}>
                <Text style={[styles.attentionTitle, { color: colors.text }]}>
                  {'Lời mời kết bạn từ "Trần Thị Mai"'}
                </Text>
                <Text style={[styles.attentionMeta, { color: colors.textMuted }]}>
                  Xác nhận để chuyển tiền P2P tức thì
                </Text>
              </View>
              <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
            </Pressable>
          </GlassCard>
        </View>

        {/* 4. TÓM TẮT NGÂN SÁCH THÁNG (MONTHLY BUDGET SUMMARY) */}
        <View style={styles.sectionWrap}>
          <View style={styles.sectionHeaderRow}>
            <Text style={[styles.sectionTitle, { color: colors.text }]}>Tóm tắt ngân sách tháng</Text>
            <Pressable
              onPress={() => router.push('/(authenticated)/planning')}
              style={({ pressed }) => [{ opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.viewAllLink, { color: colors.primary }]}>Chi tiết</Text>
            </Pressable>
          </View>

          <GlassCard style={styles.budgetCard}>
            <View style={styles.budgetRow}>
              <View>
                <Text style={[styles.budgetLabel, { color: colors.textMuted }]}>Đã chi tiêu tháng này</Text>
                <Text style={[styles.budgetSpentAmount, { color: colors.text }]}>18.250.000 ₫</Text>
              </View>
              <View style={styles.budgetRemainingCol}>
                <Text style={[styles.budgetLabel, { color: colors.textMuted }]}>Hạn mức còn lại</Text>
                <Text style={[styles.budgetRemainingAmount, { color: colors.primary }]}>6.750.000 ₫</Text>
              </View>
            </View>

            {/* Progress bar */}
            <View style={[styles.progressTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
              <View style={[styles.progressFill, { width: '68%', backgroundColor: colors.primary }]} />
            </View>

            <View style={styles.budgetFooter}>
              <Text style={[styles.budgetFooterText, { color: colors.textMuted }]}>
                Hạn mức tổng: 25.000.000 ₫ • Đã dùng 68% (còn 18 ngày)
              </Text>
            </View>
          </GlassCard>
        </View>

        {/* 5. GIAO DỊCH GẦN ĐÂY (RECENT TRANSACTIONS) */}
        <View style={styles.sectionWrap}>
          <View style={styles.sectionHeaderRow}>
            <Text style={[styles.sectionTitle, { color: colors.text }]}>Giao dịch gần đây</Text>
            <Pressable
              onPress={() => router.push('/(authenticated)/history')}
              style={({ pressed }) => [{ opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.viewAllLink, { color: colors.primary }]}>Xem tất cả</Text>
            </Pressable>
          </View>

          <View style={styles.txListWrap}>
            {recentTransactions.map((tx) => {
              const isIncome = tx.type === 'income';
              const isFund = tx.type === 'fund';

              return (
                <GlassCard key={tx.id} style={styles.recentTxRow}>
                  <View
                    style={[
                      styles.txIconWrap,
                      {
                        backgroundColor: isIncome
                          ? (isDark ? '#0F3D2E' : '#E6F8F0')
                          : isFund
                          ? (isDark ? '#2E1A47' : '#EDE9FE')
                          : (isDark ? '#3D1518' : '#FDE8E9'),
                      },
                    ]}
                  >
                    <Ionicons
                      name={
                        isIncome
                          ? 'arrow-down-outline'
                          : isFund
                          ? 'people-outline'
                          : 'arrow-up-outline'
                      }
                      size={18}
                      color={
                        isIncome
                          ? colors.primary
                          : isFund
                          ? colors.violet
                          : colors.danger
                      }
                    />
                  </View>

                  <View style={styles.txInfoWrap}>
                    <Text style={[styles.txTitle, { color: colors.text }]} numberOfLines={1}>
                      {tx.title}
                    </Text>
                    <Text style={[styles.txMeta, { color: colors.textMuted }]}>
                      {tx.time} • {tx.category}
                    </Text>
                  </View>

                  <Text
                    style={[
                      styles.txAmount,
                      {
                        color: isIncome ? colors.primary : colors.text,
                      },
                    ]}
                  >
                    {isIncome ? '+' : ''}
                    {tx.amount.toLocaleString('vi-VN')} ₫
                  </Text>
                </GlassCard>
              );
            })}
          </View>
        </View>

        {/* MODAL 1: NẠP TIỀN */}
        <Modal
          visible={quickModal === 'deposit'}
          transparent
          animationType="slide"
          onRequestClose={() => setQuickModal(null)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Nạp tiền vào Ví chính</Text>
                <Pressable onPress={() => setQuickModal(null)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.modalLabel, { color: colors.text }]}>Số tiền nạp (₫)</Text>
                <TextInput
                  value={modalAmount}
                  onChangeText={setModalAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 1000000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.modalHint, { color: colors.textMuted }]}>
                  Nguồn nạp: Ngân hàng Vietcombank liên kết (**** 9824)
                </Text>

                <Pressable
                  onPress={handleDepositSubmit}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Xác nhận nạp tiền</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>

        {/* MODAL 2: RÚT TIỀN */}
        <Modal
          visible={quickModal === 'withdraw'}
          transparent
          animationType="slide"
          onRequestClose={() => setQuickModal(null)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Rút tiền về Ngân hàng</Text>
                <Pressable onPress={() => setQuickModal(null)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.modalLabel, { color: colors.text }]}>Số tiền rút (₫)</Text>
                <TextInput
                  value={modalAmount}
                  onChangeText={setModalAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 500000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.modalHint, { color: colors.textMuted }]}>
                  Khả dụng rút tối đa: {availableBalance.toLocaleString('vi-VN')} ₫
                </Text>

                <Pressable
                  onPress={handleWithdrawSubmit}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Xác nhận rút tiền</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>

        {/* MODAL 3: GHI THU CHI THỦ CÔNG */}
        <Modal
          visible={quickModal === 'manual_tx'}
          transparent
          animationType="slide"
          onRequestClose={() => setQuickModal(null)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Ghi chép thu chi nhanh</Text>
                <Pressable onPress={() => setQuickModal(null)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.modalLabel, { color: colors.text }]}>Số tiền (₫)</Text>
                <TextInput
                  value={modalAmount}
                  onChangeText={setModalAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 65000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.modalLabel, { color: colors.text, marginTop: 12 }]}>Danh mục</Text>
                <View style={styles.categoryChips}>
                  {['Ăn uống', 'Mua sắm', 'Đi lại', 'Nhà cửa', 'Khác'].map((cat) => (
                    <Pressable
                      key={cat}
                      onPress={() => setModalCategory(cat)}
                      style={[
                        styles.chip,
                        { borderColor: modalCategory === cat ? colors.primary : colors.border },
                        modalCategory === cat && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' },
                      ]}
                    >
                      <Text style={[styles.chipText, { color: modalCategory === cat ? colors.primary : colors.textMuted }]}>
                        {cat}
                      </Text>
                    </Pressable>
                  ))}
                </View>

                <Text style={[styles.modalLabel, { color: colors.text, marginTop: 12 }]}>Ghi chú</Text>
                <TextInput
                  value={modalNote}
                  onChangeText={setModalNote}
                  placeholder="Ví dụ: Cà phê sáng..."
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleManualTxSubmit}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Lưu khoản chi</Text>
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
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  greetingCol: {
    gap: 2,
  },
  greetingSub: {
    fontSize: 13,
    fontWeight: '500',
  },
  greetingName: {
    fontSize: 20,
    fontWeight: '800',
  },
  headerActions: {
    flexDirection: 'row',
    gap: 10,
  },
  headerIconBtn: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sectionWrap: {
    gap: 8,
    marginTop: spacing.xs,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  attentionTag: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  attentionDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
  },
  countPill: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: radius.pill,
  },
  countPillText: {
    fontSize: 11,
    fontWeight: '700',
  },
  viewAllLink: {
    fontSize: 13,
    fontWeight: '700',
  },
  attentionCard: {
    paddingVertical: 4,
    paddingHorizontal: spacing.md,
  },
  attentionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    gap: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'transparent',
  },
  attentionIconWrap: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  attentionInfo: {
    flex: 1,
    gap: 2,
  },
  attentionTitle: {
    fontSize: 13,
    fontWeight: '700',
  },
  attentionMeta: {
    fontSize: 11,
  },
  budgetCard: {
    padding: spacing.md,
    gap: 10,
  },
  budgetRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  budgetLabel: {
    fontSize: 11,
  },
  budgetSpentAmount: {
    fontSize: 18,
    fontWeight: '800',
    marginTop: 2,
  },
  budgetRemainingCol: {
    alignItems: 'flex-end',
  },
  budgetRemainingAmount: {
    fontSize: 18,
    fontWeight: '800',
    marginTop: 2,
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
  budgetFooter: {
    marginTop: 2,
  },
  budgetFooterText: {
    fontSize: 11,
  },
  txListWrap: {
    gap: 8,
  },
  recentTxRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    gap: 12,
  },
  txIconWrap: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  txInfoWrap: {
    flex: 1,
    gap: 2,
  },
  txTitle: {
    fontSize: 13,
    fontWeight: '700',
  },
  txMeta: {
    fontSize: 11,
  },
  txAmount: {
    fontSize: 14,
    fontWeight: '800',
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
    gap: 6,
  },
  modalLabel: {
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
  modalHint: {
    fontSize: 12,
    marginTop: 4,
  },
  modalSubmitBtn: {
    marginTop: 18,
    paddingVertical: 14,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  modalSubmitText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  categoryChips: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginVertical: 4,
  },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
    borderWidth: 1,
  },
  chipText: {
    fontSize: 12,
    fontWeight: '600',
  },
});
